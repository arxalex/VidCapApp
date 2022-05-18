using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Capture;

namespace VidCapApp.Assets
{
    [DataContract]
    public class LEDandDisplay
    {
        [DataMember(Name = "DisplayWidth")]
        public int DisplayWidth;
        [DataMember(Name = "DisplayHeight")]
        public int DisplayHeight;
        [DataMember(Name = "Depth")]
        public int Depth;
        [DataMember(Name = "LEDVertical")]
        public int LEDVertical;
        [DataMember(Name = "LEDHorizontal")]
        public int LEDHorizontal;
        public byte brightness;
        public int Len;
        public int DLV;
        public int DLH;
        [DataMember(Name = "item")]
        public GraphicsCaptureItem item;
        [DataMember(Name = "port")]
        public string port;
        public bool? autostart;
        //private int[,] Segments;
        public LEDandDisplay(int _depth, int _ledHoriz, int _ledVert, string _port, int _DisplayWidth, int _DisplayHeight, bool? _auutsstart, byte _brightness)
        {
            Depth = _depth;
            LEDHorizontal = _ledHoriz;
            LEDVertical = _ledVert;
            port = _port;
            DisplayHeight = _DisplayHeight;
            DisplayWidth = _DisplayWidth;
            autostart = _auutsstart;
            brightness = _brightness;
            Update();
        }
        public LEDandDisplay()
        {
            Update();
        }
        public void Update()
        {
            Len = (LEDHorizontal + LEDVertical) * 2;
            DLH = DisplayWidth / LEDHorizontal;
            DLV = DisplayHeight / (LEDVertical + 2);
        }
        /*private void createLed()
        {

            Segments = new int[Len, 2];
            for (byte i = 0; i < LEDHorizontal; i++)
            {
                Segments[i, 0] = DisplayWidth / LEDHorizontal * i;
                Segments[Len - 1 - LEDVertical - i, 0] = DisplayWidth / LEDHorizontal * i;
                Segments[i, 1] = DisplayHeight - Depth;
                Segments[Len - 1 - LEDVertical - i, 1] = 0;
            }
            for (byte i = 0; i < LEDVertical; i++)
            {
                Segments[LEDHorizontal + i, 0] = DisplayWidth - Depth;
                Segments[Len - 1 - i, 0] = 0;
                Segments[LEDHorizontal + LEDVertical - 1 - i, 1] = DisplayHeight / LEDVertical * i;
                Segments[Len - LEDVertical + i, 1] = DisplayHeight / LEDVertical * i;
            }
        }
        */
    }
}
