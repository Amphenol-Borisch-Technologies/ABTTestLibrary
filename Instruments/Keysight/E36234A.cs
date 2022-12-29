﻿using System;
using System.Threading;
using Agilent.CommandExpert.ScpiNet.AgE36200_1_0_0_1_0_2_1_00; // https://www.keysight.com/us/en/search.html/command+expert
//  Test Procedure requires a voltage ramp up between 1V/µSecond through 0.5V/mSecond
//   for its VIN, Primary & Secondary Bias power supplies, powered respectively to 12.5, 6.25 & 6.25 V.
// - The VIN power supply is a Keysight E36234A, which specifies 50 milliSecond up/down programming settling to
//   within 1% of total excursion.
// - The Primary & Secondary Bias power supplies are both Keysight E36103Bs, which specify 50 milliSecond
//   up/down programming settling to within 1% of total excursion.
// - Doing the arithmetic:
//   - 12.5V ÷ 0.050S = 0.252V/mS.
//   - 6.25V ÷ 0.050S = 0.125V/mS.
// - 0.252V/mS and 0.125V/mS both lie within required voltage ramp up rate between 1V/µSecond through 0.5V/mSecond.

namespace ABTTestLibrary.Instruments.Keysight {
    // NOTE: Instruments - Channel lists aren't allowed in any methods though many, perhaps most, E36234A SCPI commands do permit them.
    public static class E36234A {
        // NOTE: Instruments - Consider using IVI driver instead of wrapping SCPI driver's calls.
        private static void ConvertChannel(Instrument Instrument, String sChannel, out Int32 iChannel) {
            iChannel = -1;
            if (Int32.TryParse(sChannel, out Int32 ic)) iChannel = --ic;
            // E36234A Channels are indexed 1 to 2, but C# arrays are indexed 0 to 1.
            // Decrement E36234A's iChannel to align to C# arrays.
            if ((iChannel != 0) && (iChannel != 1)) throw new Exception(InstrumentTasks.GetMessage(Instrument, $"Invalid Channel '{sChannel}'"));
            return;
        }

        public static void RemoteOn(Instrument Instrument) {
            ((AgE36200)Instrument.Instance).SCPI.SYSTem.REMote.Command();
        }

        public static Boolean IsOn(Instrument Instrument, String sChannel) {
            ConvertChannel(Instrument, sChannel, out Int32 iChannel);
            ((AgE36200)Instrument.Instance).SCPI.OUTPut.STATe.Query(sChannel, out Boolean[] States);
            return States[iChannel];
        }

        public static Boolean IsOff(Instrument Instrument, String sChannel) {
            return !IsOn(Instrument, sChannel);
        }

        public static void Off(Instrument Instrument, String sChannel) {
            ConvertChannel(Instrument, sChannel, out Int32 _);
            ((AgE36200)Instrument.Instance).SCPI.OUTPut.STATe.Command(false, sChannel);
        }

        public static (Double V, Double A) MeasureVA(Instrument Instrument, String sChannel) {
            ConvertChannel(Instrument, sChannel, out Int32 iChannel);
            ((AgE36200)Instrument.Instance).SCPI.MEASure.SCALar.VOLTage.DC.Query(sChannel, out Double[] V);
            ((AgE36200)Instrument.Instance).SCPI.MEASure.SCALar.CURRent.DC.Query(sChannel, out Double[] A);
            return (V[iChannel], A[iChannel]);
        }

        public static void ON(Instrument Instrument, Double Volts, Double Amps, String sChannel, Int32 SettlingDelayMS = 150) {
            ConvertChannel(Instrument, sChannel, out Int32 iChannel);
            try {
                String s;
                ((AgE36200)Instrument.Instance).SCPI.SOURce.VOLTage.LEVel.IMMediate.AMPLitude.Query("MINimum", sChannel, out Double[] V);
                ((AgE36200)Instrument.Instance).SCPI.SOURce.CURRent.LEVel.IMMediate.AMPLitude.Query("MINimum", sChannel, out Double[] A);
                if ((Volts < V[iChannel]) || (Amps < A[iChannel])) {
                    s = $"< MINimum Voltage/Current with Channel '{sChannel}'.{Environment.NewLine}";
                    s += $" - Programmed:  Voltage={Volts}/Current={Amps}.{Environment.NewLine}";
                    s += $" - Minimal   :  Voltage={V[iChannel]}/Current={A[iChannel]}.";
                    throw new Exception(InstrumentTasks.GetMessage(Instrument, s));
                }
                ((AgE36200)Instrument.Instance).SCPI.SOURce.VOLTage.LEVel.IMMediate.AMPLitude.Query("MAXimum", sChannel, out V);
                ((AgE36200)Instrument.Instance).SCPI.SOURce.CURRent.LEVel.IMMediate.AMPLitude.Query("MAXimum", sChannel, out A);
                if ((Volts < V[iChannel]) || (Amps < A[iChannel])) {
                    s = $"> MAXimum Voltage/Current with Channel '{sChannel}'.{Environment.NewLine}";
                    s += $" - Programmed:  Voltage={Volts}/Current={Amps}.{Environment.NewLine}";
                    s += $" - Maximal   :  Voltage={V[iChannel]}/Current={A[iChannel]}.";
                    throw new Exception(InstrumentTasks.GetMessage(Instrument, s));
                }
                ((AgE36200)Instrument.Instance).SCPI.SOURce.VOLTage.LEVel.IMMediate.AMPLitude.Command(Volts, sChannel);
                ((AgE36200)Instrument.Instance).SCPI.SOURce.CURRent.LEVel.IMMediate.AMPLitude.Command(Amps, sChannel);
                ((AgE36200)Instrument.Instance).SCPI.OUTPut.STATe.Command(true, sChannel);
                Thread.Sleep(SettlingDelayMS);
            } catch (Exception e) {
                throw new Exception(InstrumentTasks.GetMessage(Instrument), e);

            }
        }
    }
}