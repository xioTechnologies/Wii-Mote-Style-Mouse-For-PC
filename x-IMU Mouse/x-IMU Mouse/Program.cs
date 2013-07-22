using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;

namespace x_IMU_Mouse
{
    class Program
    {
        /// <summary>
        /// Previous state of digital ports used to interpret mouse button behaviour.
        /// </summary>
        static x_IMU_API.DigitalPortBits prevState = new x_IMU_API.DigitalPortBits();

        /// <summary>
        /// Sampled packet count used to briefly disable cursor position updates after button down.
        /// </summary>
        static int sampledCount = 0;

        /// <summary>
        /// Main method.
        /// </summary>
        /// <param name="args">
        /// Unused.
        /// </param>
        static void Main(string[] args)
        {
            Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Name + " " + Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "." + Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString());
            try
            {
                while (true)
                {
                    // Connect to x-IMU
                    Console.WriteLine("Searching for x-IMU...");
                    x_IMU_API.PortAssignment[] portAssignment = (new x_IMU_API.PortScanner(true, true)).Scan();
                    x_IMU_API.xIMUserial xIMUserial = new x_IMU_API.xIMUserial(portAssignment[0].PortName);
                    xIMUserial.QuaternionDataReceived += new x_IMU_API.xIMUserial.onQuaternionDataReceived(xIMUserial_QuaternionDataReceived);
                    xIMUserial.DigitalIODataReceived += new x_IMU_API.xIMUserial.onDigitalIODataReceived(xIMUserial_DigitalIODataReceived);
                    xIMUserial.Open();
                    Console.WriteLine("Connected to x-IMU " + portAssignment[0].DeviceID + " on " + portAssignment[0].PortName + ".");

                    // Poll for key press and detect if connection lost
                    Console.WriteLine("Press Esc to exit or any other key to set cursor centre orientation (i.e. tare).");
                    int prevCount;
                    do
                    {
                        prevCount = xIMUserial.PacketsReadCounter.TotalPackets;
                        Thread.Sleep(1000);
                        if (Console.KeyAvailable == true)
                        {
                            if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                            {
                                return;
                            }
                            xIMUserial.SendCommandPacket(x_IMU_API.CommandCodes.AlgorithmTare);
                        }
                    } while (prevCount != xIMUserial.PacketsReadCounter.TotalPackets);
                    Console.WriteLine("No data received from x-IMU.  Closing port.");
                    xIMUserial.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// QuaternionDataReceived event to set mouse position.
        /// </summary>        
        /// <remarks>
        /// Mouse position will not be updated if fewer than 32 packets received since last button down. Design assumes quaternion data output rate = 128 Hz.
        /// Cursor horizontal position over screen width is represented by ±30 degrees range of psi.
        /// Cursor vertical position over screen height is represented by ±20 degrees range of theta.
        /// </remarks>
        static void xIMUserial_QuaternionDataReceived(object sender, x_IMU_API.QuaternionData e)
        {
            if (((x_IMU_API.xIMUserial)sender).PacketsReadCounter.QuaternionDataPackets > sampledCount + 32)
            {
                float[] euler = e.ConvertToEulerAngles();
                SendInputClass.MouseEvent((int)(SendInputClass.MOUSEEVENTF.ABSOLUTE | SendInputClass.MOUSEEVENTF.MOVE),
                                          (int)(32768.5f + ((-euler[2] / 30) * 32768.5f)),
                                          (int)(32768.5f + ((-euler[1] / 20) * 32768.5f)),
                                          0);
            }
        }

        /// <summary>
        /// DigitalIODataReceived to set mouse button states.
        /// </summary>
        /// <remarks>
        /// Sets sampledCount to briefly disable cursor position updates after button down.
        /// </remarks>
        static void xIMUserial_DigitalIODataReceived(object sender, x_IMU_API.DigitalIOdata e)
        {
            if (prevState.AX1 ^ e.State.AX1)    // if left button state changed
            {
                if (e.State.AX1)
                {
                    SendInputClass.MouseEvent((int)SendInputClass.MOUSEEVENTF.LEFTDOWN, 0, 0, 0);
                    sampledCount = ((x_IMU_API.xIMUserial)sender).PacketsReadCounter.QuaternionDataPackets;
                }
                else
                {
                    SendInputClass.MouseEvent((int)SendInputClass.MOUSEEVENTF.LEFTUP, 0, 0, 0);
                }
            }
            if (prevState.AX0 ^ e.State.AX0)    // if right button state changed
            {
                if (e.State.AX0)
                {
                    SendInputClass.MouseEvent((int)SendInputClass.MOUSEEVENTF.RIGHTDOWN, 0, 0, 0);
                    sampledCount = ((x_IMU_API.xIMUserial)sender).PacketsReadCounter.QuaternionDataPackets;
                }
                else
                {
                    SendInputClass.MouseEvent((int)SendInputClass.MOUSEEVENTF.RIGHTUP, 0, 0, 0);
                }
            }
            prevState = e.State;
        }
    }
}