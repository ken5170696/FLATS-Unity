using UnityEngine;

namespace InControl
{
	public struct KeyInfo
	{
		private readonly Key key;

		private readonly string name;

		private readonly string macName;

		private readonly KeyCode[] keyCodes;

		public static readonly KeyInfo[] KeyList;

		public bool IsPressed
		{
			get
			{
				int num = keyCodes.Length;
				for (int i = 0; i < num; i++)
				{
					if (Input.GetKey(keyCodes[i]))
					{
						return true;
					}
				}
				return false;
			}
		}

		public string Name
		{
			get
			{
				if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
				{
					return macName;
				}
				return name;
			}
		}

		public Key Key
		{
			get
			{
				return key;
			}
		}

		private KeyInfo(Key key, string name, params KeyCode[] keyCodes)
		{
			this.key = key;
			this.name = name;
			macName = name;
			this.keyCodes = keyCodes;
		}

		private KeyInfo(Key key, string name, string macName, params KeyCode[] keyCodes)
		{
			this.key = key;
			this.name = name;
			this.macName = macName;
			this.keyCodes = keyCodes;
		}

		static KeyInfo()
		{
			KeyInfo[] array = new KeyInfo[111];
			
			KeyCode[] array2 = new KeyCode[1];
			array[0] = new KeyInfo(Key.None, "None", array2);
			array[1] = new KeyInfo(Key.Shift, "Shift", KeyCode.LeftShift, KeyCode.RightShift);
			array[2] = new KeyInfo(Key.Alt, "Alt", "Option", KeyCode.LeftAlt, KeyCode.RightAlt);
			array[3] = new KeyInfo(Key.Command, "Command", KeyCode.LeftCommand, KeyCode.RightCommand);
			array[4] = new KeyInfo(Key.Control, "Control", KeyCode.LeftControl, KeyCode.RightControl);
			array[5] = new KeyInfo(Key.LeftShift, "Left Shift", KeyCode.LeftShift);
			array[6] = new KeyInfo(Key.LeftAlt, "Left Alt", "Left Option", KeyCode.LeftAlt);
			array[7] = new KeyInfo(Key.LeftCommand, "Left Command", KeyCode.LeftCommand);
			array[8] = new KeyInfo(Key.LeftControl, "Left Control", KeyCode.LeftControl);
			array[9] = new KeyInfo(Key.RightShift, "Right Shift", KeyCode.RightShift);
			array[10] = new KeyInfo(Key.RightAlt, "Right Alt", "Right Option", KeyCode.RightAlt);
			array[11] = new KeyInfo(Key.RightCommand, "Right Command", KeyCode.RightCommand);
			array[12] = new KeyInfo(Key.RightControl, "Right Control", KeyCode.RightControl);
			array[13] = new KeyInfo(Key.Escape, "Escape", KeyCode.Escape);
			array[14] = new KeyInfo(Key.F1, "F1", KeyCode.F1);
			array[15] = new KeyInfo(Key.F2, "F2", KeyCode.F2);
			array[16] = new KeyInfo(Key.F3, "F3", KeyCode.F3);
			array[17] = new KeyInfo(Key.F4, "F4", KeyCode.F4);
			array[18] = new KeyInfo(Key.F5, "F5", KeyCode.F5);
			array[19] = new KeyInfo(Key.F6, "F6", KeyCode.F6);
			array[20] = new KeyInfo(Key.F7, "F7", KeyCode.F7);
			array[21] = new KeyInfo(Key.F8, "F8", KeyCode.F8);
			array[22] = new KeyInfo(Key.F9, "F9", KeyCode.F9);
			array[23] = new KeyInfo(Key.F10, "F10", KeyCode.F10);
			array[24] = new KeyInfo(Key.F11, "F11", KeyCode.F11);
			array[25] = new KeyInfo(Key.F12, "F12", KeyCode.F12);
			array[26] = new KeyInfo(Key.Key0, "Num 0", KeyCode.Alpha0);
			array[27] = new KeyInfo(Key.Key1, "Num 1", KeyCode.Alpha1);
			array[28] = new KeyInfo(Key.Key2, "Num 2", KeyCode.Alpha2);
			array[29] = new KeyInfo(Key.Key3, "Num 3", KeyCode.Alpha3);
			array[30] = new KeyInfo(Key.Key4, "Num 4", KeyCode.Alpha4);
			array[31] = new KeyInfo(Key.Key5, "Num 5", KeyCode.Alpha5);
			array[32] = new KeyInfo(Key.Key6, "Num 6", KeyCode.Alpha6);
			array[33] = new KeyInfo(Key.Key7, "Num 7", KeyCode.Alpha7);
			array[34] = new KeyInfo(Key.Key8, "Num 8", KeyCode.Alpha8);
			array[35] = new KeyInfo(Key.Key9, "Num 9", KeyCode.Alpha9);
			array[36] = new KeyInfo(Key.A, "A", KeyCode.A);
			array[37] = new KeyInfo(Key.B, "B", KeyCode.B);
			array[38] = new KeyInfo(Key.C, "C", KeyCode.C);
			array[39] = new KeyInfo(Key.D, "D", KeyCode.D);
			array[40] = new KeyInfo(Key.E, "E", KeyCode.E);
			array[41] = new KeyInfo(Key.F, "F", KeyCode.F);
			array[42] = new KeyInfo(Key.G, "G", KeyCode.G);
			array[43] = new KeyInfo(Key.H, "H", KeyCode.H);
			array[44] = new KeyInfo(Key.I, "I", KeyCode.I);
			array[45] = new KeyInfo(Key.J, "J", KeyCode.J);
			array[46] = new KeyInfo(Key.K, "K", KeyCode.K);
			array[47] = new KeyInfo(Key.L, "L", KeyCode.L);
			array[48] = new KeyInfo(Key.M, "M", KeyCode.M);
			array[49] = new KeyInfo(Key.N, "N", KeyCode.N);
			array[50] = new KeyInfo(Key.O, "O", KeyCode.O);
			array[51] = new KeyInfo(Key.P, "P", KeyCode.P);
			array[52] = new KeyInfo(Key.Q, "Q", KeyCode.Q);
			array[53] = new KeyInfo(Key.R, "R", KeyCode.R);
			array[54] = new KeyInfo(Key.S, "S", KeyCode.S);
			array[55] = new KeyInfo(Key.T, "T", KeyCode.T);
			array[56] = new KeyInfo(Key.U, "U", KeyCode.U);
			array[57] = new KeyInfo(Key.V, "V", KeyCode.V);
			array[58] = new KeyInfo(Key.W, "W", KeyCode.W);
			array[59] = new KeyInfo(Key.X, "X", KeyCode.X);
			array[60] = new KeyInfo(Key.Y, "Y", KeyCode.Y);
			array[61] = new KeyInfo(Key.Z, "Z", KeyCode.Z);
			array[62] = new KeyInfo(Key.Backquote, "Backquote", KeyCode.BackQuote);
			array[63] = new KeyInfo(Key.Minus, "Minus", KeyCode.Minus);
			array[64] = new KeyInfo(Key.Equals, "Equals", KeyCode.Equals);
			array[65] = new KeyInfo(Key.Backspace, "Backspace", "Delete", KeyCode.Backspace);
			array[66] = new KeyInfo(Key.Tab, "Tab", KeyCode.Tab);
			array[67] = new KeyInfo(Key.LeftBracket, "Left Bracket", KeyCode.LeftBracket);
			array[68] = new KeyInfo(Key.RightBracket, "Right Bracket", KeyCode.RightBracket);
			array[69] = new KeyInfo(Key.Backslash, "Backslash", KeyCode.Backslash);
			array[70] = new KeyInfo(Key.Semicolon, "Semicolon", KeyCode.Semicolon);
			array[71] = new KeyInfo(Key.Quote, "Quote", KeyCode.Quote);
			array[72] = new KeyInfo(Key.Return, "Return", KeyCode.Return);
			array[73] = new KeyInfo(Key.Comma, "Comma", KeyCode.Comma);
			array[74] = new KeyInfo(Key.Period, "Period", KeyCode.Period);
			array[75] = new KeyInfo(Key.Slash, "Slash", KeyCode.Slash);
			array[76] = new KeyInfo(Key.Space, "Space", KeyCode.Space);
			array[77] = new KeyInfo(Key.Insert, "Insert", KeyCode.Insert);
			array[78] = new KeyInfo(Key.Delete, "Delete", "Forward Delete", KeyCode.Delete);
			array[79] = new KeyInfo(Key.Home, "Home", KeyCode.Home);
			array[80] = new KeyInfo(Key.End, "End", KeyCode.End);
			array[81] = new KeyInfo(Key.PageUp, "PageUp", KeyCode.PageUp);
			array[82] = new KeyInfo(Key.PageDown, "PageDown", KeyCode.PageDown);
			array[83] = new KeyInfo(Key.LeftArrow, "Left Arrow", KeyCode.LeftArrow);
			array[84] = new KeyInfo(Key.RightArrow, "Right Arrow", KeyCode.RightArrow);
			array[85] = new KeyInfo(Key.UpArrow, "Up Arrow", KeyCode.UpArrow);
			array[86] = new KeyInfo(Key.DownArrow, "Down Arrow", KeyCode.DownArrow);
			array[87] = new KeyInfo(Key.Pad0, "Pad 0", KeyCode.Keypad0);
			array[88] = new KeyInfo(Key.Pad1, "Pad 1", KeyCode.Keypad1);
			array[89] = new KeyInfo(Key.Pad2, "Pad 2", KeyCode.Keypad2);
			array[90] = new KeyInfo(Key.Pad3, "Pad 3", KeyCode.Keypad3);
			array[91] = new KeyInfo(Key.Pad4, "Pad 4", KeyCode.Keypad4);
			array[92] = new KeyInfo(Key.Pad5, "Pad 5", KeyCode.Keypad5);
			array[93] = new KeyInfo(Key.Pad6, "Pad 6", KeyCode.Keypad6);
			array[94] = new KeyInfo(Key.Pad7, "Pad 7", KeyCode.Keypad7);
			array[95] = new KeyInfo(Key.Pad8, "Pad 8", KeyCode.Keypad8);
			array[96] = new KeyInfo(Key.Pad9, "Pad 9", KeyCode.Keypad9);
			array[97] = new KeyInfo(Key.Numlock, "Numlock", KeyCode.Numlock);
			array[98] = new KeyInfo(Key.PadDivide, "Pad Divide", KeyCode.KeypadDivide);
			array[99] = new KeyInfo(Key.PadMultiply, "Pad Multiply", KeyCode.KeypadMultiply);
			array[100] = new KeyInfo(Key.PadMinus, "Pad Minus", KeyCode.KeypadMinus);
			array[101] = new KeyInfo(Key.PadPlus, "Pad Plus", KeyCode.KeypadPlus);
			array[102] = new KeyInfo(Key.PadEnter, "Pad Enter", KeyCode.KeypadEnter);
			array[103] = new KeyInfo(Key.PadPeriod, "Pad Period", KeyCode.KeypadPeriod);
			array[104] = new KeyInfo(Key.Clear, "Clear", KeyCode.Clear);
			array[105] = new KeyInfo(Key.PadEquals, "Pad Equals", KeyCode.KeypadEquals);
			array[106] = new KeyInfo(Key.F13, "F13", KeyCode.F13);
			array[107] = new KeyInfo(Key.F14, "F14", KeyCode.F14);
			array[108] = new KeyInfo(Key.F15, "F15", KeyCode.F15);
			array[109] = new KeyInfo(Key.AltGr, "Alt Graphic", KeyCode.AltGr);
			array[110] = new KeyInfo(Key.CapsLock, "Caps Lock", KeyCode.CapsLock);
			KeyList = array;
		}
	}
}
