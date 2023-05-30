using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BGAPI2;

namespace camera
{
    public partial class Form1 : Form
    {
        bool jpeg_streaming_capable = true;
        bool unsafe_code_allowed = true;

        public Form1()
        {
            InitializeComponent();

            BGAPI2.SystemList systemList = null;
            BGAPI2.System mSystem = null;
            string sSystemID = "";

            BGAPI2.InterfaceList interfaceList = null;
            BGAPI2.Interface mInterface = null;
            string sInterfaceID = "";

            BGAPI2.DeviceList deviceList = null;
            BGAPI2.Device mDevice = null;
            string sDeviceID = "";

            DataStreamList datastreamList = null;
            BGAPI2.DataStream mDataStream = null;
            string sDataStreamID = "";

            BufferList bufferList = null;
            BGAPI2.Buffer mBuffer = null;

            try
            {
                systemList = SystemList.Instance;
                systemList.Refresh();
                System.Console.Write("5.1.2 Detected systems: {0}\n", systemList.Count);
            }
            catch (BGAPI2.Exceptions.IException ex)
            {
                System.Console.Write("ErrorType: {0}.\n", ex.GetType());
                System.Console.Write("ErrorException {0}.\n", ex.GetErrorDescription());
                System.Console.Write("in function: {0}.\n", ex.GetFunctionName());
            }

            foreach (KeyValuePair<string, BGAPI2.System> sys_pair in BGAPI2.SystemList.Instance)
            {
                sys_pair.Value.Open();
                sSystemID = sys_pair.Key;
                break;
            }

            if (sSystemID == "")
                return; // no system found
            else
                mSystem = systemList[sSystemID];

            interfaceList = mSystem.Interfaces;
            interfaceList.Refresh(100);
            System.Console.Write("5.1.4 Detected interfaces: {0}\n", interfaceList.Count);

            foreach (KeyValuePair<string, BGAPI2.Interface> ifc_pair in interfaceList)
            {
                ifc_pair.Value.Open();
                sInterfaceID = ifc_pair.Key;
                break;
            }

            if (sInterfaceID == "")
                return; // no interface found
            else
                mInterface = interfaceList[sInterfaceID];

            deviceList = mInterface.Devices;
            deviceList.Refresh(100);
            System.Console.Write("5.1.6 Detected devices: {0}\n", deviceList.Count);

            foreach (KeyValuePair<string, BGAPI2.Device> dev_pair in deviceList)
            {
                dev_pair.Value.Open();
                sDeviceID = dev_pair.Key;
                break;
            }

            if (sDeviceID == "")
                return; // no device found
            else
                mDevice = deviceList[sDeviceID];

            datastreamList = mDevice.DataStreams;
            datastreamList.Refresh();
            System.Console.Write("5.1.8 Detected datastreams: {0}\n", datastreamList.Count);

            foreach (KeyValuePair<string, BGAPI2.DataStream> dst_pair in datastreamList)
            {
                dst_pair.Value.Open();
                sDataStreamID = dst_pair.Key;
                break;
            }

            if (sDataStreamID == "")
                return; // no datastream found
            else
                mDataStream = datastreamList[sDataStreamID];

            bufferList = mDataStream.BufferList;
            for (int i = 0; i < 4; i++) // 4 buffers using internal buffers
            {
                mBuffer = new BGAPI2.Buffer();
                bufferList.Add(mBuffer);
            }
            System.Console.Write("5.1.10 Announced buffers: {0}\n", bufferList.Count);

            foreach (KeyValuePair<string, BGAPI2.Buffer> buf_pair in bufferList)
            {
                buf_pair.Value.QueueBuffer();
            }
            System.Console.Write("5.1.11. Queued buffers: {0}\n", bufferList.Count);

            mDataStream.StartAcquisition();
            mDevice.RemoteNodeList["AcquisitionStart"].Execute();


            BGAPI2.Buffer mBufferFilled = null;

            while (true)
            {
                mBufferFilled = mDataStream.GetFilledBuffer(1000); // image polling timeout 1000 msec
                if (mBufferFilled == null)
                {
                    System.Console.Write("Error: Buffer Timeout after 1000 msec\r\n");
                    break;
                }
                else if (mBufferFilled.IsIncomplete == true)
                {
                    System.Console.Write("Error: Image is incomplete\r\n");
                    // queue buffer again
                    mBufferFilled.QueueBuffer();
                }
                else
                {
                    System.Console.Write("Image {0, 5:d} received in memory address {1:X16}\r\n", mBufferFilled.FrameID, (ulong)mBufferFilled.MemPtr);

                    // JPEGTAG05 (see description on top)
                    // save the compressed images to disk if supported
                    if (jpeg_streaming_capable)
                    {
                        ulong jpeg_size = mBufferFilled.ImageLength;
                        if (jpeg_size > 0)
                        {
                            // Save jpeg data to file
                            System.IO.FileStream outfile = new System.IO.FileStream(String.Format("022_JPEGCapture_{0:D6}.jpg", mBufferFilled.FrameID),
                                System.IO.FileMode.Create, System.IO.FileAccess.Write);

                            byte[] jpeg_data = new byte[mBufferFilled.ImageLength];

                            using (MemoryStream memoryStream = new MemoryStream(jpeg_data, (int)mBufferFilled.ImageOffset, (int)mBufferFilled.ImageLength))
                            {
                                System.Drawing.Image image = System.Drawing.Image.FromStream(memoryStream);
                                pictureBox.Image = image;
                            }

                            outfile.Write(jpeg_data, 0, jpeg_data.Length);
                            outfile.Close();

                            System.Console.Write("JPEG Data found. Image saved as JPEG file.\r\n");
                        }
                        else
                        {
                            System.Console.Write("JPEG Data not found in image buffer. No JPEG Saved.\r\n");
                        }
                    }

                    // queue buffer again
                    mBufferFilled.QueueBuffer();
                }
            }


        }
    }
}
