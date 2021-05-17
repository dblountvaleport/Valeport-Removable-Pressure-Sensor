using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Modbus.Device;
using MySql.Data.MySqlClient;
using Pressure_Calibration_Software.Properties;
using Syncfusion.Windows.Forms.Chart;
using static System.String;
using Application = System.Windows.Forms.Application;
using Cursor = System.Windows.Forms.Cursor;

namespace Pressure_Calibration_Software
{
	public partial class Form1 : Form
	{
		//Software version number
		//used to compare against the version number on the website for latest version
		readonly string SoftwareVersionNumber = "1.0.0.7";
		public ModbusSerialMaster ModbusClient;
		public SerialPort Port = new SerialPort();

		//LIVE database connection 
		private const string Host = "10.0.1.3"; //IP
		private const string DBusername = "vpUserDB"; //User
		private const string DBpassword = "qMRHv2JD8yAFfLFsgUym"; //Password
		public const string Database1 = "valeport_calibration_sheets"; //Database Table

		private const string Baseurl = "http://10.0.1.3:8383/bm/"; //URL to Valeport Intranet
		public string MyConnectionString1;
		public MySqlConnection Connection1;
		public string MyConnectionString2;
		public MySqlConnection Connection2;

		public bool isExpanded;
		public bool UrlCheck = true;
		public bool ValeportConnection = false;
		public bool ConnectionFailed = false;

		//Modbus Admin Address to communicate to all functions
		public byte DeviceId = (byte) 250;

		public float MinPressure;
		public float MaxPressure;
		private string NewVersId;
		private string NewVersNo;
		public bool LoggingIn;
		public int OperatorId;

		
		public List<float> numbers;
		public float theNumber;

		public Form1()
		{

			InitializeComponent();
			//Connection string to be used when communicating with the batabase
			MyConnectionString1 = ("Server=" + Host + ";Database=" + Database1 + ";Uid=" + DBusername + ";Pwd=" + DBpassword + ";SSLMODE=NONE");
			MyConnectionString2 = ("Server=" + Host + ";Database=" + Database1 + ";Uid=" + DBusername + ";Pwd=" + DBpassword + ";SSLMODE=NONE");
			webBrowser1.DocumentCompleted += WebBrowser1_DocumentCompleted;

			//Load the previously selected option from the Internal Settings 'database' 
			if (Settings.Default.CheckLatestVers == "Always")
			{
				rbAlways.Checked = true;
			}
			if (Settings.Default.CheckLatestVers == "Never")
			{
				rbNever.Checked = true;
			}
			if (Settings.Default.CheckLatestVers == "After...")
			{
				rbAfter.Checked = true;
				dtAlert.Value = DateTime.Parse(Settings.Default.CheckLatestVersDate);
			}
		}

		private void InitializeModbus()
		{
			//create modbus/serial port
			var portName = cboComPort.SelectedItem.ToString();
			Port.PortName = portName;
			Port.BaudRate = 115200;
			Port.Parity = Parity.None;
			Port.ReadTimeout = 1000;
			Port.WriteTimeout = 1000;
			Port.Open();
			ModbusClient = ModbusSerialMaster.CreateRtu(Port);
		}

		public int GetScreenWidth()
		{
			//Return the Applications location on the primary screen
			var width = 0;
			
			foreach (var screen in Screen.AllScreens)
			{
				width += screen.Bounds.Width;
			}

			return width;
		}
		
		public int GetScreenHeight()
		{
			//Return the Applications location on the primary screen
			var height = 0;

			foreach (var screen in Screen.AllScreens)
			{
				if (screen.Bounds.Height > height)
				{
					height = screen.Bounds.Height;
				}
			}

			return height;
		}

		public int GetPrimaryLeft()
		{
			//Return the primary screens boundaries
			var left = (Screen.PrimaryScreen.Bounds.Width - this.Width) / 2;

			return left;
		}

		public int GetPrimaryTop()
		{
			//Return the primary screens boundaries
			var top = (Screen.PrimaryScreen.Bounds.Height - this.Height) / 2;

			return top;
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			MaximizeBox = false;
			Width = 672;

			pMain.BackColor = Color.FromArgb(254, 254, 254);
			pFooter.BackColor = Color.FromArgb(115, 198, 215);
			panel4.BackColor = Color.FromArgb(115, 198, 215);
			panel6.BackColor = Color.FromArgb(115, 198, 215);
			panel8.BackColor = Color.FromArgb(115, 198, 215);
			label1.ForeColor = Color.FromArgb(44, 54, 59);
			label2.ForeColor = Color.FromArgb(44, 54, 59);
			//panel12.BackColor = Color.FromArgb(229, 236, 246);
			pnlLoginScreen.BackColor = Color.FromArgb(238, 238, 238);
			pnlSettingsScreen.BackColor = Color.FromArgb(238, 238, 238);
			pnlSearch.BackColor = Color.FromArgb(238, 238, 238);
			
			var width = GetScreenWidth();
			var height = GetScreenHeight();
			var primaryLeft = GetPrimaryLeft();
			var primaryTop = GetPrimaryTop();
			
			//Set default parameters to false
			IsLogging = false;
			IsGraphing = false;

			comboBox1.SelectedIndex = 1;

			//Tool tips to display a hint/information on hover
			var toolTip1 = new ToolTip();
			toolTip1.SetToolTip(btnCopyCurrCal, @"Copy to clipboard");
			toolTip1.SetToolTip(btnCopyNewCal, @"Copy to clipboard");
			toolTip1.SetToolTip(BtnOpenSettings, @"Settings");
			toolTip1.SetToolTip(btnCollapse, @"Hide Graph");
			toolTip1.SetToolTip(btnExpand, @"Show Graph");
			toolTip1.SetToolTip(btnStartLogging, @"Start Logging");
			toolTip1.SetToolTip(btnStartGraphing, @"Start Graphing");
			toolTip1.SetToolTip(btnResetGraph, @"Reset Log");
			toolTip1.SetToolTip(btnResetLog, @"Reset Graph");
			toolTip1.SetToolTip(btnReadCalString, @"Read Cal String from Sensor");
			toolTip1.SetToolTip(btnWriteCalString, @"Write Cal String to Sensor");
			toolTip1.SetToolTip(btnSave, @"Save Data to CSV");
			toolTip1.SetToolTip(btnSaveMean, @"Save Mean to Clipboard");
			toolTip1.SetToolTip(btnResetMean, @"Reset Mean Value");
			toolTip1.SetToolTip(btnResetCal, @"Sets Calibration Parameters to 0");

			//Initialize charts Y-Axis to display 8 decimal points '0.00000000'
			chartControl1.PrimaryYAxis.RangePaddingType = ChartAxisRangePaddingType.None;
			chartControl1.PrimaryYAxis.RoundingPlaces = 8;

			//Using the returned screen boundaries and application window...
			//place application in its previous location
			//or default to centre of primary of the screen (so the application is visible)
			if (Settings.Default.Left != "0")
			{
				var left = Convert.ToInt32(Settings.Default.Left);
				left += Width / 2;
				Left = left > width ? primaryLeft : Convert.ToInt32(Settings.Default.Left);
			}

			if (Settings.Default.Top != "0")
			{
				var top = Convert.ToInt32(Settings.Default.Top);
				top += Height / 2;
				Top = top > height ? primaryTop : Convert.ToInt32(Settings.Default.Top);
			}
			
			//Load the previously selected option from the Internal Settings 'database' 
			switch (Settings.Default.Warning)
			{
				case "True":
					checkWarning.Checked = true;
					break;
				case "False":
					checkWarning.Checked = false;
					break;
			}
			
			//Load the previously selected option from the Internal Settings 'database'
			switch (Settings.Default.ShowMore)
			{
				case "True":
					Expand();
					break;
				case "False":
					Collapse();
					break;
			}
			
			//Load the previously selected option from the Internal Settings 'database'
			switch (Settings.Default.CheckLatestVers)
			{
				case "Always":
					rbAlways.Checked = true;
					break;
				case "Never":
					rbNever.Checked = true;
					break;
				case "After...":
					rbAfter.Checked = true;
					break;
			}

			//load the saved login info for BM/Akumen
			if (Settings.Default.UserName != Empty)
			{
				txtUsername.Text = Settings.Default.UserName;
				txtPassword.Text = Settings.Default.Password;
				cbSaveInfo.Checked = Settings.Default.Checked;
			}
			else
			{
				if (File.Exists(@"C:\Valeport Software\Removable Pressure Transducer\settings.txt"))
				{
					var readText = File.ReadAllText(@"C:\Valeport Software\Removable Pressure Transducer\settings.txt");
					var words = readText.Split('\r', '\n');

					txtUsername.Text = words[0];
					txtPassword.Text = words[2];
				}
			}

			//Request the list of com ports (usb devices) on the machine and add to combobox
			var ports = SerialPort.GetPortNames();
			cboComPort.Items.AddRange(ports);
			var storedComPort = Settings.Default.ComPort;
			if (storedComPort != "")
			{
				var comPortPos = Array.IndexOf(ports, storedComPort);
				if (comPortPos > -1)
				{
					cboComPort.SelectedIndex = comPortPos;
				}
			}

			numbers = new List<float>();
			
			//display software version on the application title
			this.Text += @" - " + SoftwareVersionNumber;
			
			chartControl1.Series[0].Points.Clear();

			//Check for a new version of the application
			CheckLatestVersion();
		}

		private void Disconnect()
		{
			if (IsLogging)
			{
				IsLogging = false;
				Application.DoEvents();
			}
			if (IsGraphing)
			{
				IsGraphing = false;
				Application.DoEvents();
			}
			Port.Close();
			Console.WriteLine(@"Connection closed...");
			DisableButtons();
			btnStartLogging.Enabled = false;
			btnOpenConnection.Text = @"Connect";
		}

		private void DisableButtons()
		{
			//When logging data disable buttons
			btnReadModbus.Enabled = false;
			btnWriteCalString.Enabled = false;
			btnReadCalString.Enabled = false;
			btnWriteGainOffset.Enabled = false;
			btnReadGainOffset.Enabled = false;
			btnCopyCurrCal.Enabled = false;
			btnCopyNewCal.Enabled = false;
			btnCopyCurrGain.Enabled = false;
			btnCopyNewGain.Enabled = false;
			btnSaveMean.Enabled = false;
			btnSave.Enabled = false;
			btnResetGraph.Enabled = false;
			btnResetLog.Enabled = false;
			btnResetMean.Enabled = false;
			btnStartGraphing.Enabled = false;
			btnResetCal.Enabled = false;
		}

		private void EnableButtons()
		{
			//When no longer logging data enable buttons
			btnReadModbus.Enabled = true;
			btnResetCal.Enabled = true;
			btnWriteCalString.Enabled = true;
			btnReadCalString.Enabled = true;
			btnWriteGainOffset.Enabled = true;
			btnReadGainOffset.Enabled = true;
			//btnStartLogging.Enabled = true;
			btnCopyCurrCal.Enabled = true;
			btnCopyNewCal.Enabled = true;
			btnCopyCurrGain.Enabled = true;
			btnCopyNewGain.Enabled = true;
			btnSave.Enabled = true;
			btnSaveMean.Enabled = true;
			btnResetGraph.Enabled = true;
			btnResetLog.Enabled = true;
			btnResetMean.Enabled = true;
			btnStartGraphing.Enabled = true;
		}

		public void BtnOpenConnection_Click(object sender, EventArgs e)
		{
			Cursor.Current = Cursors.WaitCursor;
			if (Port.IsOpen)
			{
				try
				{
					Disconnect();
				}
				catch (Exception exception)
				{
					Console.WriteLine(exception);
					throw;
				}
			}
			else
			{
				//Verify that a Com Port selected
				if (cboComPort.SelectedIndex != -1)
				{
					//modbusClient.SerialPort = cboComPort.SelectedItem.ToString();
					//Save selection to application settings
					Settings.Default.ComPort = cboComPort.SelectedItem.ToString();
				}
				else
				{
					MessageBox.Show(@"Unable to start connection, no Serial Port selected.");
					return;
				}

				Settings.Default.Save();
				Console.WriteLine(@"Saved selected data to log... Port: " + Settings.Default.ComPort);

				try
				{
					InitializeModbus();
					Application.DoEvents();
					if (!Port.IsOpen) return;
					btnOpenConnection.Text = @"Disconnect";
					ReadModbus();
					if (ConnectionFailed == false)
					{
						EnableButtons();
					}
					else
					{
						ConnectionFailed = false;
					}
				}
				catch (Exception ex)
				{
					DisableButtons();
					MessageBox.Show(@"Error Connecting to Port: " + Settings.Default.ComPort + @". Exeption Message: " + ex.Message);
					throw;
				}
			}

			Cursor.Current = Cursors.Default;
		}

		public static string ByteArrayToString(byte[] ba)
		{
			//Covert byte array to string
			var hex = new StringBuilder(ba.Length * 2);
			foreach (var b in ba)
				hex.AppendFormat("{0:x2}", b);
			return hex.ToString();
		}	

		private static byte[] Crc16(IReadOnlyList<byte> data)
		{
			//Calculate the CRC16 checksum for Reading / Writing Modbus commands
			byte[] checkSum = new byte[2];
			ushort regCrc = 0XFFFF;
			//remove the last 2 byte from the 8 byte byte array
			for (var i = 0; i < data.Count - 2; i++)
			{
				regCrc ^= data[i];
				for (var j = 0; j < 8; j++)
				{
					if ((regCrc & 0x01) == 1)
					{
						regCrc = (ushort) ((regCrc >> 1) ^ 0xA001);
					}
					else
					{
						regCrc = (ushort) (regCrc >> 1);
					}
				}
			}
			//Return the correct last 2 bytes for the 8 byte byte array
			checkSum[1] = (byte) ((regCrc >> 8) & 0xFF);
			checkSum[0] = (byte) (regCrc & 0xFF);
			return checkSum;
		}

		private void BtnFacebook_Click(object sender, EventArgs e)
		{
			//Open Valeports Facebook Page
			Process.Start("https://www.facebook.com/ValeportLtd");
		}

		private void BtnTwitter_Click(object sender, EventArgs e)
		{
			//Open Valeports Twitter Page
			Process.Start("https://twitter.com/ValeportLtd");
		}

		private void BtnBrowser_Click(object sender, EventArgs e)
		{
			//Open Valeports Website
			Process.Start("https://www.valeport.co.uk/");
		}

		private void BtnYoutube_Click(object sender, EventArgs e)
		{
			//Open Valeports Youtube Page
			Process.Start("https://www.youtube.com/user/ValeportUK");
		}

		private void BtnLinkedin_Click(object sender, EventArgs e)
		{
			//Open Valeports Linkedin Page
			Process.Start("https://www.linkedin.com/company/valeport/");
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			//Save form location and state of users options to internal application database
			Settings.Default.ShowMore = isExpanded ? "True" : "False";
			Settings.Default.Top = Top.ToString();
			Settings.Default.Left = Left.ToString();
			Settings.Default.Save();
			Application.Exit();
		}

		private void BtnExpand_Click(object sender, EventArgs e)
		{
			Expand();
			//Ensure that any of the visible pop-up windows/panels remain in the centre of the application window after the form has resized
			if (pnlSettingsScreen.Visible)
			{
				pnlSettingsScreen.Left = (Width / 2) - (pnlSettingsScreen.Width / 2);
			}
			if (pnlLoginScreen.Visible)
			{
				pnlLoginScreen.Left = (Width / 2) - (pnlLoginScreen.Width / 2);
			}
			if (pnlSearch.Visible)
			{
				pnlSearch.Left = (Width / 2) - (pnlSearch.Width / 2);
			}
		}

		private void Expand()
		{
			//Show Chart
			isExpanded = true;
			Width = 1419; //large application window
			button3.Visible = false;
			btnLogout.Visible = true;
			btnLogout2.Visible = false;
			btnSearch.Visible = true;
			btnSearch2.Visible = false;
			chartControl1.Visible = true;
			BtnOpenSettings.Visible = true;
			btnCollapse.Visible = true;
			btnExpand.Visible = false;
		}

		private void BtnCollapse_Click(object sender, EventArgs e)
		{
			Collapse();
			//Ensure that any of the visible pop-up windows/panels remain in the centre of the application window after the form has resized
			if (pnlSettingsScreen.Visible)
			{
				pnlSettingsScreen.Left = (Width / 2) - (pnlSettingsScreen.Width / 2);
			}
			if (pnlLoginScreen.Visible)
			{
				pnlLoginScreen.Left = (Width / 2) - (pnlLoginScreen.Width / 2);
			}
			if (pnlSearch.Visible)
			{
				pnlSearch.Left = (Width / 2) - (pnlSearch.Width / 2);
			}
		}

		private void Collapse()
		{
			//Hide Chart
			isExpanded = false;
			Width = 672; //small application window
			button3.Visible = true;
			btnLogout.Visible = false;
			btnLogout2.Visible = true;
			btnSearch.Visible = false;
			btnSearch2.Visible = true;
			chartControl1.Visible = false;
			BtnOpenSettings.Visible = false;
			btnExpand.Visible = true;
			btnCollapse.Visible = false;
		}

		private void SendRequest(byte function, params byte[] parameters)
		{
			//Read from Modbus Register
			var anything = new List<byte> {DeviceId, function};

			//Build command parameters
			anything.AddRange(parameters);

			//Add 2 empty params for the checksum
			anything.Add(0);
			anything.Add(0);

			//Convert list of bytes to byte array
			var frame = anything.ToArray();

			//Calculate the checksum of the parameters
			var checkSum = Crc16(frame);

			//Replace empty parameters with calculated checksum
			anything[anything.Count - 1] = (checkSum[0]);
			anything[anything.Count - 2] = (checkSum[1]);

			frame = anything.ToArray();

			if (Port.IsOpen)
			{
				//Write complete message to the port
				Port.Write(frame, 0, frame.Length);
			}
		}

		private void SendWrite(byte function, byte command, params byte[] data)
		{
			//Write to Modbus Register
			var anything = new List<byte> {DeviceId, function, command};

			//Build command parameters
			anything.AddRange(data);
			
			//Add 2 empty params for the checksum
			anything.Add(0);
			anything.Add(0);
			
			//Convert list of bytes to byte array
			var frame = anything.ToArray();
			
			//Calculate the checksum of the parameters
			var checkSum = Crc16(frame);
			
			//Replace empty parameters with calculated checksum
			anything[anything.Count - 1] = (checkSum[0]);
			anything[anything.Count - 2] = (checkSum[1]);

			frame = anything.ToArray();

			if (Port.IsOpen)
			{
				//Write complete message to the port
				Port.Write(frame, 0, frame.Length);
			}
		}

		private bool CalculateCheckSum(byte[] data)
		{
			//Calculate CRC16 checksum using the first 6 bytes of the byte array
			//Then compare the calculated check sum to the last 2 bytes of the byte array
			//If they match then the command was correctly sent/recieved
			var responceFrame = new byte[data.Length];

			for (var i = 0; i < data.Length - 2; i++)
			{
				responceFrame[i] = data[i];
			}

			var responcecheckSum = Crc16(responceFrame);
			var crc1 = data[data.Length - 1];
			var crc2 = data[data.Length - 2];

			if (responcecheckSum[0] == crc1 && responcecheckSum[1] == crc2) return true;
			MessageBox.Show(@"Check Sums do not match...");
			return false;
		}

		private void BtnReadModbus_Click(object sender, EventArgs e)
		{
			Cursor.Current = Cursors.WaitCursor;
			ReadModbus();
			Cursor.Current = Cursors.Default;
		}

		private void ReadModbus()
		{
			//Read all information from all functions
			try
			{
				//Read Address
				var param66 = new byte[] { 1 };
				Function66(param66);
				
				Application.DoEvents();
				////Read Configuration
				//var param32 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33 };
				//Function32(param32);
				
				Application.DoEvents();
				//Read Device Type & Software Version
				var param48 = new byte[] {};
				Function48(param48);
				
				Application.DoEvents();
				//Read Serail No
				var param69 = new byte[] {};
				Function69(param69);

				Application.DoEvents();
				//Read Offset, Gain, Min & Max Pressure, Cal 2, Cal 1, Cal 0, Cal Date, QR Serial No, PolyCalFlag...
				var param30 = new byte[] { 64,65,80,81,100,101,102,107,108 };   
				Function30(param30);

				Application.DoEvents();
				//Read Pressure & Temperature from Sensor 1
				var param73 = new byte[] { 0,1,2,3,4,5 }; 	
				Function73(param73);

				Application.DoEvents();
			}
			catch (Exception exception)
			{
				Console.WriteLine($@"ReadModbus Process Failed. Exception: {exception.Message}");
			}
		}

		private void Function30(IEnumerable<byte> param)
		{
			//Function 30 - Reads Calibration info
			const byte function = 0x1E;
			foreach (var parameter in param)
			{
				Port.DiscardInBuffer();
				Port.DiscardOutBuffer();

				SendRequest(function, parameter);
				Thread.Sleep(100);
				var responseArray = GetResponse(function);
				Thread.Sleep(100);
				TranslateFunc30(responseArray, parameter);
				Thread.Sleep(100);
			}
		}

		private float TranslateFunc30(byte[] responseArray, byte parameter)
		{
			//Translate Function 30 - Reads Calibration info
			if (responseArray == null || responseArray.Length <= 0)
				return -99;
			switch (responseArray[0])
			{
				case 2:
					Console.WriteLine(@"Parameter no. > 111");
					break;
				case 3:
					Console.WriteLine(@"Message length is incorrect");
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised");
					break;
			}

			//Translate value of a channel
			var b3 = responseArray[0];
			var b2 = responseArray[1];
			var b1 = responseArray[2];
			var b0 = responseArray[3];

			var f = new byte[] { b0, b1, b2, b3 };

			var floatVal = FromFloatSafe(f);

			switch (parameter)
			{
				case 64:
					//Offset
					var offset = floatVal;
					lblOffset.Text = offset.ToString(CultureInfo.InvariantCulture);
					Application.DoEvents();
					break;
				case 65:
					//Gain
					var gain = floatVal;
					lblGain.Text = gain.ToString(CultureInfo.InvariantCulture);
					txtCurrentGainString.Text = lblGain.Text + @";" + lblOffset.Text; 
					Application.DoEvents();
					break;
				case 80:
					//Min Press
					MinPressure = floatVal;
					Application.DoEvents();
					break;
				case 81:
					//Max Press
					MaxPressure = floatVal;
					lblPressureRange.Text = MinPressure.ToString(CultureInfo.InvariantCulture) + @" - " + MaxPressure.ToString(CultureInfo.InvariantCulture) + @" Meters";
					Application.DoEvents();
					break;
				case 100:
					//Cal 2
					var cal2 = floatVal;
					lblCalItem3.Text = cal2.ToString(CultureInfo.InvariantCulture);
					Application.DoEvents();
					break;
				case 101:
					//Cal 1
					var cal1 = floatVal;
					lblCalItem2.Text = cal1.ToString(CultureInfo.InvariantCulture);
					Application.DoEvents();
					break;
				case 102:
					//Cal 0
					var cal0 = floatVal;
					lblCalItem1.Text = cal0.ToString(CultureInfo.InvariantCulture);
					//lblCalItem1.Text = string.Format("{0:#.######E+00}", cal0);
					txtCurrentCalString.Text = lblCalItem1.Text + @";" + lblCalItem2.Text + @";" + lblCalItem3.Text; 
					Application.DoEvents();
					break;
				case 107:
					//Qr / Valeport Serial Number
					var serialNumber = Convert.ToInt64(floatVal);
					lblVPSerialNo.Text = serialNumber.ToString(CultureInfo.InvariantCulture);
					Application.DoEvents();
					break;
				case 108:
					//Polynomial Cal Flag
					var polyCalFlag = floatVal;
					switch (polyCalFlag)
					{
						case 0:
							comboBox1.SelectedIndex = 1;
							break;
						case 1:
							comboBox1.SelectedIndex = 0;
							break;
					}
					Application.DoEvents();
					break;
			}

			return floatVal;

			//var responce = new BitArray(responseArray);
		}
		
		private void Function31(byte param, float data)
		{
			//Write new Cal String
			const byte function = 0x1F;
			Port.DiscardInBuffer();
			Port.DiscardOutBuffer();
			
			//var floatVal = float.Parse(data);
			var ieee = XdrFloat(data);

			//translate 'data' to 4 byte array... opposite of:
			//var floatVal = FromFloatSafe(f);

			//Do write here...

			SendWrite(function, param, ieee);
			Thread.Sleep(100);
			var responseArray = GetResponse(function);
			Thread.Sleep(100);
			TranslateFunc31(responseArray, param);
			Thread.Sleep(100);
		}

		public static byte[] XdrFloat(float value) {
			byte[] bytes = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		private void TranslateFunc31(byte[] responseArray, byte parameter)
		{
			if (responseArray == null || responseArray.Length <= 0) return;
			switch (responseArray[0])
			{
				case 2:
					Console.WriteLine(@"Write access is not allowed");
					break;
				case 3:
					Console.WriteLine(@"Message length is incorrect");
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised");
					break;
			}

			switch (parameter)
			{
				case 100:
					break;
				case 101:
					break;
				case 102:
					break;
			}

			//var responce = new BitArray(responseArray);
		}

		private void Function32(IEnumerable<byte> param)
		{
			//Not Used
			//Read Configuration
			const byte function = 0x20;
			foreach (var parameter in param)
			{
				Port.DiscardInBuffer();
				Port.DiscardOutBuffer();

				SendRequest(function, parameter);
				Thread.Sleep(100);
				var responseArray = GetResponse(function);
				Thread.Sleep(100);
				TranslateFunc32(responseArray);
				Thread.Sleep(100);
			}
		}

		private void TranslateFunc32(byte[] responseArray)
		{
			//Not Used
			if (responseArray == null || responseArray.Length <= 0) return;
			switch (responseArray[0])
			{
				case 2:
					Console.WriteLine(@"Desired parameter no. is not available");
					break;
				case 3:
					Console.WriteLine(@"Message length is incorrect");
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised");
					break;
			}

			var responce = new BitArray(responseArray);
		}

		private void Function33(IEnumerable<byte> param)
		{
			//Not Used
			//Write Configuration
			const byte function = 0x21;
			foreach (var parameter in param)
			{
				Port.DiscardInBuffer();
				Port.DiscardOutBuffer();

				SendRequest(function, parameter);
				Thread.Sleep(100);
				var responseArray = GetResponse(function);
				Thread.Sleep(100);
				TranslateFunc33(responseArray);
				Thread.Sleep(100);
			}
		}

		private void TranslateFunc33(byte[] responseArray)
		{
			//Not Used
			if (responseArray == null || responseArray.Length <= 0) return;
			switch (responseArray[0])
			{
				case 2:
					Console.WriteLine(@"Write access is not allowed");
					break;
				case 3:
					Console.WriteLine(@"Message length is incorrect");
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised");
					break;

			}

			var responce = new BitArray(responseArray);
		}

		private void Function48(IEnumerable<byte> param)
		{
			Port.DiscardInBuffer();
			Port.DiscardOutBuffer();

			//Read Device Type & Software Version
			const byte function = 0x30;
			SendRequest(function);
			Thread.Sleep(100);
			var responseArray = GetResponse(function);
			Thread.Sleep(100);
			TranslateFunc48(responseArray);
			Thread.Sleep(100);
		}

		private void TranslateFunc48(byte[] responseArray)
		{
			if (responseArray == null || responseArray.Length <= 0) return;
			switch (responseArray[0])
			{
				case 3:
					Console.WriteLine(@"Message length is incorrect");
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised");
					break;
			}

			//Translate Device Type & Software Version
			var theclass = responseArray[0];
			var group = responseArray[1];
			lblDeviceType.Text = theclass + @"." + group;
			if (theclass == 5 && group == 21)
			{
				lblDeviceType.Text = theclass + @"." + group + @" (S30X2)";
			}

			var year = responseArray[2];
			var week = responseArray[3];
			lblSoftVers.Text = year + @"." + week;
			var buf = responseArray[4]; //not used
			var stat = responseArray[5]; //not used
		}

		private void Function66(IEnumerable<byte> param)
		{
			//Read Device Address
			const byte function = 0x42;
			foreach (var parameter in param)
			{
				Port.DiscardInBuffer();
				Port.DiscardOutBuffer();

				SendRequest(function, parameter);
				Thread.Sleep(100);
				var responseArray = GetResponse(function);
				Thread.Sleep(100);
				TranslateFunc66(responseArray);
				Thread.Sleep(100);
			}
		}

		private void TranslateFunc66(byte[] responseArray)
		{
			if (responseArray == null || responseArray.Length <= 0)
			{
				Disconnect();
				DisableButtons();
				ConnectionFailed = true;
				MessageBox.Show(@"No Instrument Found... Check Com Port Settings / Connection!");
				return;
			}
			switch (responseArray[0])
			{
				case 3:
					Console.WriteLine(@"Message length is incorrect");
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised");
					break;
			}

			//Translate Device Address
			var value = Convert.ToInt32(ByteArrayToString(responseArray), 16);
			lblDeviceAddress.Text = value.ToString();
		}

		private void Function69(IEnumerable<byte> param)
		{
			//Read Serial No
			Port.DiscardInBuffer();
			Port.DiscardOutBuffer();

			const byte function = 0x45;
			SendRequest(function);
			Thread.Sleep(100);
			var responseArray = GetResponse(function);
			Thread.Sleep(100);
			TranslateFunc69(responseArray);
			Thread.Sleep(100);
		}

		private void TranslateFunc69(byte[] responseArray)
		{
			if (responseArray == null || responseArray.Length <= 0) return;
			switch (responseArray[0])
			{
				case 3:
					Console.WriteLine(@"Message length is incorrect");
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised");
					break;
			}

			//Translate Serial No
			var value = Convert.ToInt32(ByteArrayToString(responseArray), 16);
			lblSerialNo.Text = value.ToString();
		}

		private void Function73(IEnumerable<byte> param)
		{
			//Read Pressure & Temperature values from channel
			const byte function = 0x49;
			foreach (var parameter in param)
			{
				Port.DiscardInBuffer();
				Port.DiscardOutBuffer();
				if (Port.IsOpen)
				{
					SendRequest(function, parameter);
					Thread.Sleep(100);
					var responseArray = GetResponse(function);
					Thread.Sleep(100);
					TranslateFunc73(responseArray, parameter);
					Thread.Sleep(100);
				}
			}
		}

		public static float FromFloatSafe(byte[] f)
		{
			var fb = BitConverter.ToInt32(f, 0);

			var sign = ((fb >> 31) & 1);
			var exponent = ((fb >> 23) & 0xFF);
			var mantissa = (fb & 0x7FFFFF);

			float fMantissa;
			var fSign = sign == 0 ? 1.0f : -1.0f;

			if (exponent != 0)
			{
				exponent -= 127;
				fMantissa = 1.0f + (mantissa / (float)0x800000);
			}
			else
			{
				if (mantissa != 0)
				{
					// denormal
					exponent -= 126;
					fMantissa = 1.0f / 0x800000;
				}
				else
				{
					// +0 and -0 cases
					fMantissa = 0;
				}
			}

			var fExponent = (float)Math.Pow(2.0, exponent);
			var floatVal = fSign * fMantissa * fExponent;
			return floatVal;
		}

		private float TranslateFunc73(byte[] responseArray, byte parameter)
		{
			if (responseArray == null || responseArray.Length <= 0)
			{
				return -99;
			}

			switch (responseArray[0])
			{
				case 2:
					Console.WriteLine(@"CH > 5 version 5.20-XX.XX and if CH > 11 version 5.21-XX.XX..." + responseArray[0]);
					break;
				case 3:
					Console.WriteLine(@"Message length is incorrect..." + responseArray[0]);
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised..." + responseArray[0]);
					break;
			}

			//Translate value of a channel
			var b3 = responseArray[0];
			var b2 = responseArray[1];
			var b1 = responseArray[2];
			var b0 = responseArray[3];
			var stat = responseArray[4];


			var f = new byte[] { b0, b1, b2, b3 };

			var floatVal = FromFloatSafe(f);

			switch (parameter)
			{
				case 0:
					//CH0 - Calculated channel?
					var ch0 = floatVal;
					Application.DoEvents();
					break;
				case 1:
					//P1 - Pressure from pressure sensor 1 (bar)
					var p1 = floatVal;
					lblCurrentPressure.Text = p1.ToString(CultureInfo.InvariantCulture);
					Application.DoEvents();
					break;
				case 2:
					//P2 - Pressure from pressure sensor 2 (bar)
					var p2 = floatVal;
					Application.DoEvents();
					break;
				case 3:
					//T - Additional temperature sensor (°C)
					var t = floatVal;
					Application.DoEvents();
					break;
				case 4:
					//TOB1 - Temperature of pressure sensor 1 (°C)
					var tob1 = floatVal;
					lblCurrentTemp.Text = tob1.ToString(CultureInfo.InvariantCulture);
					Application.DoEvents();
					break;
				case 5:
					//TOB2 - Temperature of pressure sensor 2 (°C)
					var tob2 = floatVal;
					Application.DoEvents();
					break;
			}

			return floatVal;
		}


		private void Function74(IEnumerable<byte> param)
		{
			//Not Used
			Port.DiscardInBuffer();
			Port.DiscardOutBuffer();

			const byte function = 0x4A;
			SendRequest(function, 1);
			Thread.Sleep(100);
			var responseArray = GetResponse(function);
			Thread.Sleep(100);
			TranslateFunc74(responseArray);
			Thread.Sleep(100);
		}

		private void TranslateFunc74(byte[] responseArray)
		{
			//Not Used
			if (responseArray == null || responseArray.Length <= 0) return;
			switch (responseArray[0])
			{
				case 2:
					Console.WriteLine(@"CH > 5");
					break;
				case 3:
					Console.WriteLine(@"Message length is incorrect");
					break;
				case 4:
					Console.WriteLine(@"Class.Group -Year.Week = 5.20-5.50 and earlier");
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised");
					break;
			}

			var B3 = responseArray[0];
			var B2 = responseArray[1];
			var B1 = responseArray[2];
			var B0 = responseArray[3];
			var STAT = responseArray[4];
		}
		

		private void Function75(IEnumerable<byte> param)
		{
			//Not Used
			Port.DiscardInBuffer();
			Port.DiscardOutBuffer();

			const byte function = 0x4B;
			SendRequest(function);
			Thread.Sleep(100);
			var responseArray = GetResponse(function);
			Thread.Sleep(100);
			TranslateFunc75(responseArray);
			Thread.Sleep(100);
		}

		private void TranslateFunc75(byte[] responseArray)
		{
			//Not Used
			if (responseArray == null || responseArray.Length <= 0) return;
			switch (responseArray[0])
			{
				case 2:
					Console.WriteLine(@"CH > 5");
					break;
				case 3:
					Console.WriteLine(@"Message length is incorrect");
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised");
					break;
			}

			var p1H = responseArray[0];
			var p1L = responseArray[1];
			var tH = responseArray[2];
			var tL = responseArray[3];
		}

		private void Function95(IEnumerable<byte> param)
		{
			//Not Used
			Port.DiscardInBuffer();
			Port.DiscardOutBuffer();

			const byte function = 0x5F;
			SendRequest(function, 1);
			Thread.Sleep(100);
			var responseArray = GetResponse(function);
			Thread.Sleep(100);
			TranslateFunc95(responseArray);
			Thread.Sleep(100);
		}

		private void TranslateFunc95(byte[] responseArray)
		{
			//Not Used
			if (responseArray == null || responseArray.Length <= 0) return;
			switch (responseArray[0])
			{
				case 1:
					Console.WriteLine(@"In Power-up mode");
					break;
				case 2:
					Console.WriteLine(@"CMD invalid");
					break;
				case 3:
					Console.WriteLine(@"Message length is incorrect");
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised");
					break;
			}

			//
		}
		
		private void Function100(IEnumerable<byte> param)
		{
			//Not Used
			Port.DiscardInBuffer();
			Port.DiscardOutBuffer();

			const byte function = 0x64;
			SendRequest(function, 2);
			Thread.Sleep(100);
			var responseArray = GetResponse(function);
			Thread.Sleep(100);
			TranslateFunc100(responseArray);
			Thread.Sleep(100);
		}

		private void TranslateFunc100(byte[] responseArray)
		{
			//Not Used
			if (responseArray == null || responseArray.Length <= 0) return;
			switch (responseArray[0])
			{
				case 2:
					Console.WriteLine(@"Index > 8");
					break;
				case 3:
					Console.WriteLine(@"Message length is incorrect");
					break;
				case 32:
					Console.WriteLine(@"Device has not yet been initialised");
					break;
			}

			var para0 = responseArray[0];
			var para1 = responseArray[1];
			var para2 = responseArray[2];
			var para3 = responseArray[3];
			var para4 = responseArray[4];
		}

		private byte[] GetResponse(byte function)
		{
			if (Port.IsOpen)
			{
				if (Port.BytesToRead > 0)
				{
					var data = new byte[Port.BytesToRead];

					Port.Read(data, 0, data.Length);

					var newdata = new byte[0];
					if (!CalculateCheckSum(data)) return newdata;
					if (data.Length <= 4) return newdata;
					newdata = new byte[data.Length - 4];
					Buffer.BlockCopy(data, 2, newdata, 0, newdata.Length);
					return newdata;
				}
				else
				{
					return new byte[0];
				}
			}
			else
			{
				return new byte[0];
			}
		}

		private void BtnReadCalString_Click(object sender, EventArgs e)
		{
			Cursor.Current = Cursors.WaitCursor;
			Application.DoEvents();
			//Read Cal 2, Cal 1, Cal 0...
			var param30 = new byte[] { 100,101,102 };   
			Function30(param30);
			Application.DoEvents();
		}

		private void BtnReadGainOffset_Click(object sender, EventArgs e)
		{
			Cursor.Current = Cursors.WaitCursor;
			Application.DoEvents();
			//Read Offset, Gain...
			var param30 = new byte[] { 64,65 };   
			Function30(param30);
			Application.DoEvents();
		}

		private void BtnWriteCalString_Click(object sender, EventArgs e)
		{
			//example cal string
			//#085;6.193428E-06;1.000281E+00;-6.361230E-04
			
			Cursor.Current = Cursors.WaitCursor;
			//string[] parts = txtNewCalString.Text.Split(';');
			float[] parts = Array.ConvertAll(txtNewCalString.Text.Split(';'), float.Parse);

			var length = parts.Length;
			if (length > 3)
			{
				MessageBox.Show(@"Ensure '#' command is removed from start of Cal String");
			}
			switch (length)
			{
				case 1:
					MessageBox.Show(@"Cal String Array is empty");
					break;
				case 2:
					MessageBox.Show(@"Cal String Array is too short");
					break;
				case 3:
				//cmd
			{
					for (int i = 0; i < parts.Length; i++)
					{
						if (i == 0)
						{
							//write cal string part 1
							Application.DoEvents();
							Function31(102, parts[i]);
						}
						if (i == 1)
						{
							//write cal string part 2
							Application.DoEvents();
							Function31(101, parts[i]);
						}
						if (i == 2)
						{
							//write cal string part 3
							Application.DoEvents();
							Function31(100, parts[i]);
						}
					}
					
					//Write Polynomial Flag
					Application.DoEvents();
					Function31(108, 1);

					//Read with Func30...
					Application.DoEvents();
					//Read Cal 2, Cal 1, Cal 0
					var param30 = new byte[] { 100,101,102 };
					Function30(param30);
					Application.DoEvents();

					//If connected to Valeport Network insert recorded data to database
					if (ValeportConnection)
					{
						StoreWrittenCalibration();
					}

					break;
				}
				case 4:
					MessageBox.Show(@"Cal String Array is too long");
					break;

			}
		}

		private void StoreWrittenCalibration()
		{
			const string insertQuery = "INSERT INTO written_calibrations ( DateTime, InstrumentSerialNo, KellerSerialNo, OperatorId, PressureRange, Gain, Offset, CalData1, CalData2, CalData3 ) VALUES ( @DateTime, @InstrumentSerialNo, @KellerSerialNo, @OperatorId, @PressureRange, @Gain, @Offset, @CalData1, @CalData2, @CalData3 );";
			Console.WriteLine(insertQuery);

			using (var command = new MySqlCommand(insertQuery, Connection1))
			{
				command.Parameters.AddWithValue("@DateTime", DateTime.Now);
				command.Parameters.AddWithValue("@InstrumentSerialNo", lblVPSerialNo.Text);
				command.Parameters.AddWithValue("@KellerSerialNo", lblSerialNo.Text);
				command.Parameters.AddWithValue("@PressureRange", lblPressureRange.Text);
				command.Parameters.AddWithValue("@OperatorId", OperatorId);
				command.Parameters.AddWithValue("@Gain", lblGain.Text);
				command.Parameters.AddWithValue("@Offset", lblOffset.Text);
				command.Parameters.AddWithValue("@CalData1", lblCalItem1.Text);
				command.Parameters.AddWithValue("@CalData2", lblCalItem2.Text);
				command.Parameters.AddWithValue("@CalData3", lblCalItem3.Text);
				var dbData = command.ExecuteReader();

				if (dbData.HasRows)
				{
					dbData.Read();
				}
				else
				//Check Error
				{
					Console.WriteLine(@"# line 3382");
				}

				dbData.Close();
			}
		}

		private void BtnWriteGainOffset_Click(object sender, EventArgs e)
		{
			//example gain offset
			//#035;64949;1;10000;0
			
			Cursor.Current = Cursors.WaitCursor;
			var parts = Array.ConvertAll(txtNewGainString.Text.Split(';'), float.Parse);
			
			var length = parts.Length;
			if (length > 2)
			{
				MessageBox.Show(@"Ensure '#' command is removed from start of Gain Offset");
			}
			switch (length)
			{
				case 1:
					MessageBox.Show(@"Gain / Offset Array is empty");
					break;
				//cmd
				case 2:
				{
					for (var i = 0; i < parts.Length; i++)
					{
						switch (i)
						{
							case 0:
								//write gain
								Application.DoEvents();
								Function31(65, parts[i]);
								break;
							case 1:
								//write offset
								Application.DoEvents();
								Function31(64, parts[i]);
								break;
						}
					}

					//Write Polynomial Flag
					Application.DoEvents();
					Function31(108, 0);

					//Read Gain / Offset
					Application.DoEvents();
					var param30 = new byte[] { 64,65 };
					Function30(param30);
					Application.DoEvents();

					//If connected to Valeport Network insert recorded data to database
					if (ValeportConnection)
					{
						StoreWrittenCalibration();
					}

					break;
				}
				case 3:
					MessageBox.Show(@"Too many Gain / Offset parameters");
					break;
			}
		}

		public bool IsLogging;
		public bool IsGraphing;

		private void BtnStartLogging_Click(object sender, EventArgs e)
		{
			if (btnStartLogging.Text == @"Stop Logging")
			{
				IsLogging = false;
				btnStartLogging.Text = @"Start Logging";
			}
			else
			{
				IsLogging = true;
				btnStartLogging.Text = @"Stop Logging";
			}
		}

		private float ReadFunc73Values(byte parameter, byte function)
		{
			Port.DiscardInBuffer();
			Port.DiscardOutBuffer();

			var param73 = new[] {parameter};
			Function73(param73);

			SendRequest(function, parameter);
			Application.DoEvents();
			Thread.Sleep(100);
			var responseArray = GetResponse(function);
			Application.DoEvents();
			Thread.Sleep(100);
			var currentReading = TranslateFunc73(responseArray, parameter);
			Application.DoEvents();


			return currentReading;
		}

		private async void BtnSave_Click(object sender, EventArgs e)
		{
			if (listView1.Items.Count > 0)
			{
				Cursor.Current = Cursors.WaitCursor;
				using (var sfd = new SaveFileDialog() {Filter = @"CSV|*.csv", ValidateNames = true})
				{
					if (sfd.ShowDialog() != DialogResult.OK) return;
					using (var sw = new StreamWriter(new FileStream(sfd.FileName, FileMode.Create),
						Encoding.UTF8))
					{
						var sb = new StringBuilder();
						sb.AppendLine("Time,Pressure (Bar)");
						foreach (ListViewItem item in listView1.Items)
						{
							var item1 = Format(CultureInfo.InvariantCulture, "{0:0.000000}", item.SubItems[0].Text);
							var item2 = Format(CultureInfo.InvariantCulture, "{0:0.000000}", item.SubItems[1].Text);
							sb.AppendLine($"{item1},{item2}");
						}

						await sw.WriteLineAsync(sb.ToString());
						MessageBox.Show(@"Your data has been sucessfully exported.", @"Message", MessageBoxButtons.OK,
							MessageBoxIcon.Information);
					}
				}

				Cursor.Current = Cursors.Default;
			}
		}

		private void BtnCopyCurrCal_Click(object sender, EventArgs e)
		{
			if (txtCurrentCalString.Text != null)
			{
				Clipboard.SetText(txtCurrentCalString.Text);
			}
		}

		private void BtnCopyNewCal_Click(object sender, EventArgs e)
		{
			if (txtNewCalString.Text != null)
			{
				Clipboard.SetText(txtNewCalString.Text);
			}
		}

		private void BtnOpenSettings_Click(object sender, EventArgs e)
		{
			if (pnlSettingsScreen.Visible != true)
			{
				pnlSettingsScreen.Left = (Width / 2) - (pnlSettingsScreen.Width / 2);
				pnlSettingsScreen.Top = (Height / 2) - (pnlSettingsScreen.Height / 2) - 50;
				pnlSettingsScreen.BringToFront();
				pnlSettingsScreen.Visible = true;
			}
			else
			{
				if (rbAlways.Checked == false && rbNever.Checked == false && rbAfter.Checked == false)
				{
					MessageBox.Show(@"Please select an option.");
				}
				else
				{
					pnlSettingsScreen.SendToBack();
					pnlSettingsScreen.Visible = false;
				}
			}
		}

		private void rbAfter_CheckedChanged(object sender, EventArgs e)
		{
			if (rbAfter.Checked)
			{
				dtAlert.Enabled = true;
			}
			else
			{
				dtAlert.Enabled = false;
			}
		}
		
		private void CheckLatestVersion()
		{
			//Establish Resource & URL for VPT Software
			const string urlAddress = "https://valeport.download/checkversno.php?Resource=62";
			var data = "";
			try
			{
				//Navigate to web page
				var request = (HttpWebRequest)WebRequest.Create(urlAddress);
				//Read web page data
				var response = (HttpWebResponse)request.GetResponse();

				//If web page successfully loads read all data to 'data'
				if (response.StatusCode == HttpStatusCode.OK)
				{
					var receiveStream = response.GetResponseStream();
					StreamReader readStream = null;

					readStream = IsNullOrWhiteSpace(response.CharacterSet) ?
									new StreamReader(receiveStream) : new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));

					data = readStream.ReadToEnd();

					response.Close();
					readStream.Close();
				}
				Console.WriteLine(data);

				if (data.Contains(@"<body>") && data.Contains(@"</body>"))
				{
					var start = data.IndexOf(@"<body>", 0, StringComparison.Ordinal) + @"<body>".Length;
					var end = data.IndexOf(@"</body>", start, StringComparison.Ordinal);
					var theBody = data.Substring(start, end - start);
					theBody = theBody.Trim();
					var latestVals = theBody.Split(',');

					NewVersId = latestVals[0];
					NewVersNo = latestVals[1];

					var latestVersParts = latestVals[1].Split('.').Select(int.Parse).ToArray();
					var thisVers = SoftwareVersionNumber;
					var thisVersParts = thisVers.Split('.').Select(int.Parse).ToArray();

					var newVers = latestVersParts[0] > thisVersParts[0];

					if (latestVersParts[0] == thisVersParts[0])
					{
						if (latestVersParts[1] > thisVersParts[1])
						{
							newVers = true;
						}

						if (latestVersParts[1] == thisVersParts[1])
						{
							if (latestVersParts[2] > thisVersParts[2])
							{
								newVers = true;
							}

							if (latestVersParts[2] == thisVersParts[2])
							{
								if (latestVersParts[3] > thisVersParts[3])
								{
									newVers = true;
								}
							}
						}
					}
					if (newVers)
					{
						//New version of software available...
						Console.WriteLine(@"New Vers");

						var doNewVers = false;
						//Read the users user settings
						var theCheckLatestVers = Settings.Default.CheckLatestVers;
						if (theCheckLatestVers == "")
						{
							doNewVers = true;
						}

						//Presnt user with notification...
						//If the user has set to always recieved update notifications
						if (theCheckLatestVers == "Always")
						{
							doNewVers = true;
						}
						
						//If the user has set to only recieve updates after a duration of time
						if (theCheckLatestVers == "After...")
						{
							if (Settings.Default.CheckLatestVersDate != "")
							{
								if (DateTime.Now.Date > DateTime.Parse(Settings.Default.CheckLatestVersDate))
								{
									doNewVers = true;
								}
							}
						}

						if (doNewVers)
						{
							var result = MessageBox.Show(@"There is an Update available." + "\n\n" +
							                             @"Current Version: " + SoftwareVersionNumber + "\n" +
							                             @"New Version: " + NewVersNo + "\n\n" +
							                             @"Would you like to update?",
								@"Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.None, 
								MessageBoxDefaultButton.Button1, (MessageBoxOptions)0x40000);

							if (result == DialogResult.Yes)
							{
								Process.Start("https://valeport.download/DoDownloadFile.php?Version=" + NewVersId);
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.WriteLine(e.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
				
			if (checkWarning.Checked)
			{
				Settings.Default.Warning = "True";
				Settings.Default.Save();
			}
			else
			{
				Settings.Default.Warning = "False";
				Settings.Default.Save();
			}
			if (rbAlways.Checked == false && rbNever.Checked == false && rbAfter.Checked == false)
			{
				MessageBox.Show(@"Please select an option.");
			}
			else
			{
				if (rbAlways.Checked)
				{
					Settings.Default.CheckLatestVers = "Always";
					Settings.Default.CheckLatestVersDate = "";
					Settings.Default.Save();
					pnlSettingsScreen.SendToBack();
					pnlSettingsScreen.Visible = false;

					return;
				}
				if (rbNever.Checked)
				{
					Settings.Default.CheckLatestVers = "Never";
					Settings.Default.CheckLatestVersDate = "";
					Settings.Default.Save();
					pnlSettingsScreen.SendToBack();
					pnlSettingsScreen.Visible = false;

					return;
				}
				if (rbAfter.Checked)
				{
					Settings.Default.CheckLatestVers = "After...";
					Settings.Default.CheckLatestVersDate = dtAlert.Value.ToShortDateString();
					Settings.Default.Save();
					pnlSettingsScreen.SendToBack();
					pnlSettingsScreen.Visible = false;
					
					return;
				}
			}
		}

		private void btnSaveMean_Click(object sender, EventArgs e)
		{
			Cursor.Current = Cursors.WaitCursor;
			btnSaveMean.Enabled = false;
			if (listView1.Items.Count > 0)
			{
				//Calculate mean pressure value for all data in listbox...
				var mean = 0.0f;
				var theData = "";
				foreach (ListViewItem item in listView1.Items)
				{
					var datetime = Format(CultureInfo.InvariantCulture, item.SubItems[0].Text);
					var pressure = Format(CultureInfo.InvariantCulture, "{0:0.000000}", item.SubItems[1].Text);
					theData = theData + "'DateTime':" + datetime + ",'Pressure':" + pressure + ";";
					mean += Convert.ToSingle(pressure);
				}
				mean /= listView1.Items.Count;

				//If connected to Valeport Network insert recorded data to database
				if (ValeportConnection)
				{
					const string insertQuery = "INSERT INTO logged_data ( DateTime, InstrumentSerialNo, KellerSerialNo, OperatorId, PressureRange, PressureMean, Data, Gain, Offset, CalData1, CalData2, CalData3 ) VALUES ( @DateTime, @InstrumentSerialNo, @KellerSerialNo, @OperatorId, @PressureRange, @PressureMean, @Data, @Gain, @Offset, @CalData1, @CalData2, @CalData3 );";
					Console.WriteLine(insertQuery);

					using (var command = new MySqlCommand(insertQuery, Connection1))
					{
						command.Parameters.AddWithValue("@DateTime", DateTime.Now);
						command.Parameters.AddWithValue("@InstrumentSerialNo", lblVPSerialNo.Text);
						command.Parameters.AddWithValue("@KellerSerialNo", lblSerialNo.Text);
						command.Parameters.AddWithValue("@PressureRange", lblPressureRange.Text);
						command.Parameters.AddWithValue("@PressureMean", mean);
						command.Parameters.AddWithValue("@Data", theData);
						command.Parameters.AddWithValue("@OperatorId", OperatorId);
						command.Parameters.AddWithValue("@Gain", lblGain.Text);
						command.Parameters.AddWithValue("@Offset", lblOffset.Text);
						command.Parameters.AddWithValue("@CalData1", lblCalItem1.Text);
						command.Parameters.AddWithValue("@CalData2", lblCalItem2.Text);
						command.Parameters.AddWithValue("@CalData3", lblCalItem3.Text);
						var dbData = command.ExecuteReader();

						if (dbData.HasRows)
						{
							dbData.Read();
						}
						else 
						//Check Error
						{
							Console.WriteLine(@"# line 4493");
						}

						dbData.Close();
					}
				}
				
				//Save mean to clipboard
				Clipboard.SetText(mean.ToString(CultureInfo.InvariantCulture));
				//display message box informing the user the data have been saved to clipboard
				AutoClosingMessageBox.Show("Data copied to clipboard... " + mean.ToString(CultureInfo.InvariantCulture), "Mean Pressure", 4500);
			}
			btnSaveMean.Enabled = true;
			Cursor.Current = Cursors.Default;
		}

		private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (comboBox1.SelectedItem.ToString() == "2nd Order Polynomial")
			{
				panel2.Visible = true;
				panel5.Visible = false;
			}

			if (comboBox1.SelectedItem.ToString() == "Straight Line")
			{
				panel2.Visible = false;
				panel5.Visible = true;
			}
		}

		private void btnCopyCurrGain_Click(object sender, EventArgs e)
		{
			if (txtCurrentGainString.Text != null)
			{
				Clipboard.SetText(txtCurrentGainString.Text);
			}
		}

		private void btnCopyNewGain_Click(object sender, EventArgs e)
		{
			if (txtNewGainString.Text != null)
			{
				Clipboard.SetText(txtNewGainString.Text);
			}
		}

		private void btnResetMean_Click(object sender, EventArgs e)
		{
			//Reset the current mean value
			lblMeanPressure.Text = @"Pressure Mean: 0.000000000";
		}

		private void btnResetGraph_Click(object sender, EventArgs e)
		{
			//Clear the charts data
			chartControl1.Series[0].Points.Clear();
		}

		private void btnResetLog_Click(object sender, EventArgs e)
		{
			//Reset the list of logged data
			listView1.Items.Clear();
			//Reset the current mean value
			lblMeanPressure.Text = @"Pressure Mean: 0.000000000";
		}

		private void btnStartGraphing_Click(object sender, EventArgs e)
		{
			if (btnStartGraphing.Text == @"Stop Graphing")
			{
				IsGraphing = false;
				btnStartGraphing.Text = @"Start Graphing";
				EnableButtons();
				btnOpenConnection.Enabled = true;
				btnStartLogging.Enabled = false;
				btnOpenConnection.Enabled = true;
			}
			else
			{
				if (!Port.IsOpen) return;
				btnStartGraphing.Text = @"Stop Graphing";
				
				//Disable buttons when graphing to prevent the user for sending commands while logging data
				btnReadModbus.Enabled = false;
				btnWriteCalString.Enabled = false;
				btnReadCalString.Enabled = false;
				btnWriteGainOffset.Enabled = false;
				btnReadGainOffset.Enabled = false;
				btnCopyCurrCal.Enabled = false;
				btnCopyNewCal.Enabled = false;
				btnResetCal.Enabled = false;
				btnCopyCurrGain.Enabled = false;
				btnCopyNewGain.Enabled = false;
				btnOpenConnection.Enabled = false;
				btnStartLogging.Enabled = true;
				btnSave.Enabled = false;
				
				IsGraphing = true;
				//when Graphing is selected add datapoints to chartControl
				while (IsGraphing)
				{
					Invoke(new MethodInvoker(delegate()
					{
						const byte function = 0x49;

						//Add Datapoints to chartControl
						var currentpressure = ReadFunc73Values(1, function);
						chartControl1.Series[0].Points.Add(DateTime.Now.ToString("HH:mm:ss"), currentpressure);
						Application.DoEvents();

						var calculatedPressure = Format(CultureInfo.InvariantCulture, "{0:0.000000}", currentpressure.ToString(CultureInfo.InvariantCulture));

						//when Logging is selected add data to the listbox & calculate the standard deviation & mean pressure.
						if (!IsLogging) return;
						//Calculate Standard Deviation
						//float calculatedStandardDeviation;
						//numbers.Add(currentpressure);
						//if (listView1.Items.Count == 0)
						//{
						//	calculatedStandardDeviation = currentpressure;
						//}
						//else
						//{
						//	calculatedStandardDeviation = CalculateStandardDeviation(numbers);
						//	//calculatedStandardDeviation = numbers.Average();
						//}

						//Add data to listbox 
						string[] row =
						{
							DateTime.Now.ToString(CultureInfo.InvariantCulture),
							calculatedPressure,
							//calculatedStandardDeviation.ToString(CultureInfo.InvariantCulture)
						};

						var listViewItem = new ListViewItem(row);
						listView1.Items.Add(listViewItem);
						listView1.Items[listView1.Items.Count - 1].EnsureVisible();

						Application.DoEvents();

						//Calculate the Mean value of the logged pressure
						var mean = 0.0f;
						for (var index = 0; index < listView1.Items.Count; index++)
						{
							var item = listView1.Items[index];
							var temp = Format(CultureInfo.InvariantCulture, "{0:0.000000}", item.SubItems[1].Text);
							mean += Convert.ToSingle(temp);
						}

						lblMeanPressure.Text = @"Pressure Mean: " + (mean / listView1.Items.Count).ToString(CultureInfo.InvariantCulture);

						Application.DoEvents();
					}));
				}
			}
		}

		private float CalculateStandardDeviation(IReadOnlyCollection<float> value)
		{   
			float standardDeviation = 0;

			if (value.Any()) 
			{      
				//Calculate the average
				var avg = value.Average();

				//Perform the Sum of (value-avg)_2_2    
				var sum = value.Sum(d => Math.Pow(d - avg, 2));

				//Put it all together
				standardDeviation = ((float)Math.Sqrt(sum) / (value.Count()-1));   
			}  

			return standardDeviation;
		}

		private void btnResetCal_Click(object sender, EventArgs e)
		{
			if (checkWarning.Checked)
			{
				var result = MessageBox.Show(
					@"Are you sure you wish to clear the existing calibration from the device?",
					@"Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
					MessageBoxDefaultButton.Button1, (MessageBoxOptions) 0x40000);

				if (result == DialogResult.Yes)
				{
					ResetCalibration();
				}
			}
			else
			{
				ResetCalibration();
			}
		}

		private void ResetCalibration()
		{
			//Clear & set a parameters to 0 or their default values

			Cursor.Current = Cursors.WaitCursor;
			//write cal string part 1
			Application.DoEvents();
			Function31(102, 0);

			//write cal string part 2
			Application.DoEvents();
			Function31(101, 1);

			//write cal string part 3
			Application.DoEvents();
			Function31(100, 0);

			//Reset gain
			Application.DoEvents();
			Function31(65, 1);

			//Reset offset
			Application.DoEvents();
			Function31(64, 0);

			//Reset Polynomial Flag
			Application.DoEvents();
			Function31(108, 1);

			ReadModbus();
			Cursor.Current = Cursors.Default;
		}

		private void WebBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
		{
			//MessageBox.Show(e.Url.ToString());

			if (webBrowser1.DocumentText.Contains("Sorry, cannot connect to Akumen Business Manager system"))
			{
				MessageBox.Show(@"Failed to load page. Try again.");
			}

			if (UrlCheck)
			{
				//Check if the user is connected to the Valeport network
				if (e.Url.ToString() == Baseurl)
				{
					//If the user is on the valeport network, allow them to login - to create a way of logging calibration data
					UrlCheck = false;
					ValeportConnection = true;

					pnlLoginScreen.Left = (Width - pnlSettingsScreen.Width) / 2;
					pnlLoginScreen.Top = ((Height - pnlSettingsScreen.Height) / 2) - 50;
					pnlLoginScreen.BringToFront();
					pnlLoginScreen.Visible = true;
					
					ConnectToDatabase();
				}
				else
				{
					//If the user is not on the valeport network, do not save any of their data!
					UrlCheck = false;
					btnOpenConnection.Enabled = true;
					ValeportConnection = false;
				}
			}

			if (LoggingIn)
			{
				if (e.Url.ToString() == Baseurl + "bmhome.php")
				{
					//Once successfully logged in, obtain their information 
					LoggingIn = false;
					pnlLoginScreen.Visible = false;
					btnOpenConnection.Enabled = true;
					ScrapeAkumenForLoginData();
				}
			}
		}

		private void ConnectToDatabase()
		{
			try
			{
				if (MyConnectionString1 == null) return;
				Console.WriteLine(@"Connected to " + Database1);
				Connection1 = new MySqlConnection(MyConnectionString1);
				Connection1.Open();
			}
			catch
			{
				MessageBox.Show(@"Unable to connect to database: " + Database1 + @" on IP: " + Host);
				Close();
			}
			try
			{
				if (MyConnectionString2 == null) return;
				Console.WriteLine(@"Connected to " + Database1);
				Connection2 = new MySqlConnection(MyConnectionString2);
				Connection2.Open();
			}
			catch
			{
				MessageBox.Show(@"Unable to connect to database: " + Database1 + @" on IP: " + Host);
				Close();
			}
		}

		private void btnLogin_Click(object sender, EventArgs e)
		{
			try
			{
				Connection1 = new MySqlConnection(MyConnectionString1);
				Connection1.Open();
				Connection2 = new MySqlConnection(MyConnectionString2);
				Connection2.Open();
				//save login info for BM/Akumen
				if (cbSaveInfo.Checked)
				{
					Settings.Default.UserName = txtUsername.Text;
					Settings.Default.Password = txtPassword.Text;
					Settings.Default.Checked = cbSaveInfo.Checked;
					Settings.Default.Save();
				}
				LogIn();
				
				System.IO.Directory.CreateDirectory(@"C:\Valeport Software\Removable Pressure Transducer\");

				using (StreamWriter writer = new StreamWriter(@"C:\Valeport Software\Removable Pressure Transducer\settings.txt"))  
				{  
					writer.WriteLine(txtUsername.Text);  
					writer.WriteLine(txtPassword.Text);  
				}  
			}
			catch
			{
				MessageBox.Show(@"Unable to connect to Akumen.");
			}
		}

		private void LogIn()
		{
			//Log into the Valeport intranet/internal database page
			var userComplete = false;
			var passComplete = false;
			if (webBrowser1.Document != null)
			{
				//Scrape the valeport akumen page for the username & password text boxes, in in the parameters and click the 'submit' button
				var htmlDoc = webBrowser1.Document;
				foreach (HtmlElement pageElement in htmlDoc.All)
				{
					//Scrape web page for element with the TagName 'INPUT'
					if (pageElement.TagName == "INPUT")
					{
						//Scrape TagName 'INPUT' for 'name'
						if (!string.Equals(pageElement.GetAttribute("name"), null, StringComparison.Ordinal))
						{
							//Scrape for the 'login' textbox
							if (pageElement.GetAttribute("name").Equals("login"))
							{
								//Change the attributes parameter
								pageElement.SetAttribute("value", txtUsername.Text);
								userComplete = true;
							}
							//Scrape for the 'password' textbox
							if (pageElement.GetAttribute("name").Equals("password"))
							{
								//Change the attributes parameter
								pageElement.SetAttribute("value", txtPassword.Text);
								passComplete = true;
							}

							//When both elements are filled in, scrape & click the login button
							if (userComplete && passComplete)
							{
								if (pageElement.GetAttribute("name").Equals("Submit"))
								{
									pageElement.InvokeMember("Click");
									LoggingIn = true;
									userComplete = false;
									passComplete = false;
								}
							}
						}
					}
				}
			}
		}

		private void ScrapeAkumenForLoginData()
		{
			//Once logged in, scrape the main menu for users information to aquire the users Signature
			if (webBrowser1.Document != null)
			{
				var htmlDoc = webBrowser1.Document;
				foreach (HtmlElement pageElement in htmlDoc.All)
				{
					if (pageElement.InnerText != null)
					{
						//Scrape the valeport akumen page for users username
						var sText = @"Akumen Business Manager User: ";
						if (pageElement.InnerText.Contains(sText))
						{
							var pageText = pageElement.InnerText;
							var pos = pageText.IndexOf(sText, StringComparison.Ordinal);
							pageText = pageText.Remove(0, pos + sText.Length);
							sText = "\r\n";
							pos = pageText.IndexOf(sText, StringComparison.Ordinal);
							pageText = pageText.Remove(pos, pageText.Length - pos);
							lblCurrentUser.Text += sText + pageText;
							lblCurrentUser.Visible = true;
							GetOperatorByName(pageText);
							return;
						}
					}
				}
			}
		}

		private void AddOperator(string name)
		{
			//Insert new Operator to database if the login details match the Akumen database
			const string insertQuery = "INSERT INTO operator ( AkumenId , AkumenName, SignatureSize, Signature ) VALUES ( @AkumenId, @AkumenName, @SignatureSize, @Signature );";
			Console.WriteLine(insertQuery);

			using (var command = new MySqlCommand(insertQuery, Connection2))
			{
				command.Parameters.AddWithValue("@AkumenId", "0");
				command.Parameters.AddWithValue("@AkumenName", name);
				command.Parameters.AddWithValue("@SignatureSize", "0");
				command.Parameters.AddWithValue("@Signature", "0");
				var dbData = command.ExecuteReader();

				if (dbData.HasRows)
				{
					dbData.Read();
				}
				else 
					//Check Error
				{
					Console.WriteLine(@"# line 4465");
				}

				dbData.Close();
			}
		}

		private void GetOperatorByName(string name)
		{
			//Use users username to obtain the users information (digital signature, to be logged along side a calibration)
			const string query = "SELECT o.Id, o.AkumenId, o.AkumenName, o.SignatureSize, o.Signature FROM operator o WHERE o.AkumenName = @name";
			using (var command = new MySqlCommand(query, Connection1))
			{
				command.Parameters.AddWithValue("@name", name);
				using (var reader = command.ExecuteReader())
				{
					if (!reader.HasRows)
					{
						//If user is not on the Operator table, but the user is a Valeport employee, add them to the database
						AddOperator(name);
						//Enable the log out & search buttons
						btnLogout.Enabled = true;
						btnLogout2.Enabled = true;
						btnSearch.Enabled = true;
						btnSearch2.Enabled = true;
						webBrowser1.Navigate("http://10.0.1.3:8383/bm/");
					}
					else
					{
						while (reader.Read())
						{
							var newOperator = new Operator
							{
								Id = reader.GetInt32("Id"),
								AkumenId = reader.GetInt32("AkumenId"),
								AkumenName = reader.GetString("AkumenName"),
								SignatureSize = reader.GetInt32("SignatureSize")
							};

							var rawData = new byte[newOperator.SignatureSize];

							//If Operator does not have a signature image/blob stored in the database, do not display the signature.
							if (newOperator.SignatureSize > 0)
							{
								reader.GetBytes(reader.GetOrdinal("Signature"), 0, rawData, 0,
									length: newOperator.SignatureSize);

								var ms = new MemoryStream(rawData);
								var outImage = new Bitmap(ms);

								ms.Close();
								ms.Dispose();

								newOperator.Signature = outImage;
								pictureBox1.Image = outImage;
								pictureBox1.Visible = true;
							}

							//Enable the log out & search buttons
							OperatorId = newOperator.Id;
							btnLogout.Enabled = true;
							btnLogout2.Enabled = true;
							btnSearch.Enabled = true;
							btnSearch2.Enabled = true;
							webBrowser1.Navigate("http://10.0.1.3:8383/bm/");
						}
					}
				}
			}
		}

		private string GetOperatorIdByName(string name)
		{
			const string query = "SELECT o.Id, o.AkumenId, o.AkumenName, o.SignatureSize, o.Signature FROM operator o WHERE o.AkumenName = @name";
			using (var command = new MySqlCommand(query, Connection2))
			{
				command.Parameters.AddWithValue("@name", name);
				using (var reader = command.ExecuteReader())
				{
					if (!reader.HasRows)
					{
						//MessageBox.Show(@"Error with MySQL. Error 8392." + (char) 10 + (char) 13 + @"Unable to locate name: '" + name + @"' on table: operator.");
					}
					else
					{
						while (reader.Read())
						{
							var akumenId = reader.GetString("Id");
							return akumenId;
						}
					}
				}
			}

			return "";
		}
		
		private string GetOperatorById(string id)
		{
			//Not used, not needed...

			const string query = "SELECT o.Id, o.AkumenId, o.AkumenName, o.SignatureSize, o.Signature FROM operator o WHERE o.Id = @id";
			using (var command = new MySqlCommand(query, Connection2))
			{
				command.Parameters.AddWithValue("@id", id);
				using (var reader = command.ExecuteReader())
				{
					if (!reader.HasRows)
					{
						MessageBox.Show(@"Error with MySQL. Error 8393." + (char) 10 + (char) 13 + @"Unable to locate id: '" + id + @"' on table: operator.");
					}
					else
					{
						while (reader.Read())
						{
							var akumenName = reader.GetString("AkumenName");
							return akumenName;
						}
					}
				}
			}

			return "";
		}

		private void SearchDatabase(string table, string column, string parameter)
		{
			dataGridView1.Rows.Clear();
			dataGridView1.Columns.Clear();
			var query = "SELECT * FROM " + table + " WHERE " + column + " = @parameter";
			using (var command = new MySqlCommand(query, Connection2))
			{
				command.Parameters.AddWithValue("@parameter", parameter);
				using (var reader = command.ExecuteReader())
				{
					if (!reader.HasRows)
					{
						MessageBox.Show(@"Error with MySQL. Error 8393." + (char) 10 + (char) 13 + @"Unable to locate " + column + @": '" + parameter + @"' on table: " + table + @".");
					}
					else
					{
						switch (table)
						{
							case "written_calibrations":
								dataGridView1.Columns.Add("1","Id");
								dataGridView1.Columns.Add("2","DateTime");
								dataGridView1.Columns.Add("3","InstrumentSerialNo");
								dataGridView1.Columns.Add("4","KellerSerialNo");
								dataGridView1.Columns.Add("5","OperatorId");
								dataGridView1.Columns.Add("6","PressureRange");
								dataGridView1.Columns.Add("7","Gain");
								dataGridView1.Columns.Add("8","Offset");
								dataGridView1.Columns.Add("9","CalData1");
								dataGridView1.Columns.Add("10","CalData2");
								dataGridView1.Columns.Add("11","CalData3");
								while (reader.Read())
								{
									//display on datagridview...
									dataGridView1.Rows.Add(reader.GetString("Id"), reader.GetString("DateTime"), reader.GetString("InstrumentSerialNo"), reader.GetString("KellerSerialNo"), reader.GetString("OperatorId"), reader.GetString("PressureRange"), reader.GetString("Gain"), reader.GetString("Offset"), reader.GetString("CalData1"), reader.GetString("CalData2"), reader.GetString("CalData3"));
								}
								break;
							case "logged_data":
								dataGridView1.Columns.Add("1","Id");
								dataGridView1.Columns.Add("2","DateTime");
								dataGridView1.Columns.Add("3","InstrumentSerialNo");
								dataGridView1.Columns.Add("4","KellerSerialNo");
								dataGridView1.Columns.Add("5","OperatorId");
								dataGridView1.Columns.Add("6","PressureRange");
								dataGridView1.Columns.Add("7","PressureMean");
								dataGridView1.Columns.Add("8","Gain");
								dataGridView1.Columns.Add("9","Offset");
								dataGridView1.Columns.Add("10","CalData1");
								dataGridView1.Columns.Add("11","CalData2");
								dataGridView1.Columns.Add("12","CalData3");
								while (reader.Read())
								{
									//display on datagridview...
									dataGridView1.Rows.Add(reader.GetString("Id"), reader.GetString("DateTime"), reader.GetString("InstrumentSerialNo"), reader.GetString("KellerSerialNo"), reader.GetString("OperatorId"), reader.GetString("PressureRange"), reader.GetString("PressureMean"), reader.GetString("Gain"), reader.GetString("Offset"), reader.GetString("CalData1"), reader.GetString("CalData2"), reader.GetString("CalData3"));
								}
								break;
						}
					}
				}
			}
			//dataGridView1.Columns[0].Visible = false;
			dataGridView1.ClearSelection();
		}

		private void BtnLogout_Click(object sender, EventArgs e)
		{
			pnlLoginScreen.Left = (Width / 2) - (pnlLoginScreen.Width / 2);
			pnlLoginScreen.BringToFront();
			btnLogout.Enabled = false;
			btnOpenConnection.Enabled = false;
			btnLogout2.Enabled = false;
			btnSearch.Enabled = false;
			btnSearch2.Enabled = false;
			lblCurrentUser.Visible = false;
			pnlLoginScreen.Visible = true;
			pictureBox1.Visible = false;
			lblCurrentUser.Text = @"Welcome, ";
			webBrowser1.Navigate(Baseurl + "index.php");
		}

		private void BtnSearch_Click(object sender, EventArgs e)
		{
			if (pnlSearch.Visible != true)
			{
				pnlSearch.Left = (Width / 2) - (pnlSearch.Width / 2);
				pnlSearch.BringToFront();
				pnlSearch.Visible = true;
			}
			else
			{
				pnlSearch.SendToBack();
				pnlSearch.Visible = false;
			}
		}

		private void BtnSearchDatabase_Click(object sender, EventArgs e)
		{
			if (txtSearchParam.Text != "")
			{
				if (cmbSearchBy.SelectedIndex > 0)
				{
					if (cmbTable.SelectedIndex > 0)
					{
						var table = "";
						var parameter = txtSearchParam.Text;
						switch (cmbTable.SelectedIndex)
						{
							case 1:
								table = "written_calibrations";
								break;
							case 2:
								table = "logged_data";
								break;
						}

						switch (cmbSearchBy.SelectedIndex)
						{
							//Valeport Serial Number
							case 1:
								SearchDatabase(table, "InstrumentSerialNo" , parameter);
								break;
							//Keller Serial Number
							case 2:
								SearchDatabase(table, "KellerSerialNo" , parameter);
								break;
							//Operator Name
							case 3:
								var theId = GetOperatorIdByName(txtSearchParam.Text);
								SearchDatabase(table, "OperatorId" , theId);
								break;
						}
					}
					else
					{
						MessageBox.Show(@"'Search By' field is empty.");
					}
				}
				else
				{
					MessageBox.Show(@"'Search By' field is empty.");
				}
			}
			else
			{
				MessageBox.Show(@"'Search Parameter' field is empty.");
			}
		}
	}

	public class Operator
	{
		public int Id;
		public int AkumenId;
		public string AkumenName;
		public int SignatureSize;
		public Bitmap Signature;
	}

	public class AutoClosingMessageBox {
		private readonly System.Threading.Timer _timeoutTimer;
		private readonly string _caption;

		private AutoClosingMessageBox(string text, string caption, int timeout) 
		{
			_caption = caption;
			_timeoutTimer = new System.Threading.Timer(OnTimerElapsed,
				null, timeout, System.Threading.Timeout.Infinite);
			using(_timeoutTimer)
				MessageBox.Show(text, caption);
		}

		public static void Show(string text, string caption, int timeout) 
		{
			new AutoClosingMessageBox(text, caption, timeout);
		}

		private void OnTimerElapsed(object state) 
		{
			var mbWnd = FindWindow("#32770", _caption); // lpClassName is #32770 for MessageBox
			if(mbWnd != IntPtr.Zero)
				SendMessage(mbWnd, WmClose, IntPtr.Zero, IntPtr.Zero);
			_timeoutTimer.Dispose();
		}

		const int WmClose = 0x0010;
		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
		[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
		private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
	}
}