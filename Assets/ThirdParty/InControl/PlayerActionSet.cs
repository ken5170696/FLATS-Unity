using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
namespace InControl
{
	public abstract class PlayerActionSet
	{
		private const ushort currentDataFormatVersion = 2;

		public BindingSourceType LastInputType;

		public ulong LastInputTypeChangedTick;

		public InputDeviceClass LastDeviceClass;

		public InputDeviceStyle LastDeviceStyle;

		private List<PlayerAction> actions;

		private List<PlayerOneAxisAction> oneAxisActions;

		private List<PlayerTwoAxisAction> twoAxisActions;

		private Dictionary<string, PlayerAction> actionsByName;

		private BindingListenOptions listenOptions;

		internal PlayerAction listenWithAction;

		private InputDevice activeDevice;

		public InputDevice Device { get; set; }

		public List<InputDevice> IncludeDevices { get; private set; }

		public List<InputDevice> ExcludeDevices { get; private set; }

		public ReadOnlyCollection<PlayerAction> Actions { get; private set; }

		public ulong UpdateTick { get; protected set; }

		public bool Enabled { get; set; }

		public bool PreventInputWhileListeningForBinding { get; set; }

		public object UserData { get; set; }

		public PlayerAction this[string actionName]
		{
			get
			{
				PlayerAction value;
				if (actionsByName.TryGetValue(actionName, out value))
				{
					return value;
				}
				throw new KeyNotFoundException("Action '" + actionName + "' does not exist in this action set.");
			}
		}

		public bool IsListeningForBinding
		{
			get
			{
				return listenWithAction != null;
			}
		}

		public BindingListenOptions ListenOptions
		{
			get
			{
				return listenOptions;
			}
			set
			{
				listenOptions = value ?? new BindingListenOptions();
			}
		}

		public InputDevice ActiveDevice
		{
			get
			{
				if (activeDevice != null)
				{
					return activeDevice;
				}
				return InputDevice.Null;
			}
		}

		public event Action<BindingSourceType> OnLastInputTypeChanged;

		protected PlayerActionSet()
		{
			actions = new List<PlayerAction>();
			oneAxisActions = new List<PlayerOneAxisAction>();
			twoAxisActions = new List<PlayerTwoAxisAction>();
			actionsByName = new Dictionary<string, PlayerAction>();
			listenOptions = new BindingListenOptions();

			Enabled = true;
			PreventInputWhileListeningForBinding = true;
			Device = null;
			IncludeDevices = new List<InputDevice>();
			ExcludeDevices = new List<InputDevice>();
			Actions = new ReadOnlyCollection<PlayerAction>(actions);
			InputManager.AttachPlayerActionSet(this);
		}

		public void Destroy()
		{
			this.OnLastInputTypeChanged = null;
			InputManager.DetachPlayerActionSet(this);
		}

		protected PlayerAction CreatePlayerAction(string name)
		{
			return new PlayerAction(name, this);
		}

		internal void AddPlayerAction(PlayerAction action)
		{
			action.Device = FindActiveDevice();
			if (actionsByName.ContainsKey(action.Name))
			{
				throw new InControlException("Action '" + action.Name + "' already exists in this set.");
			}
			actions.Add(action);
			actionsByName.Add(action.Name, action);
		}

		protected PlayerOneAxisAction CreateOneAxisPlayerAction(PlayerAction negativeAction, PlayerAction positiveAction)
		{
			PlayerOneAxisAction playerOneAxisAction = new PlayerOneAxisAction(negativeAction, positiveAction);
			oneAxisActions.Add(playerOneAxisAction);
			return playerOneAxisAction;
		}

		protected PlayerTwoAxisAction CreateTwoAxisPlayerAction(PlayerAction negativeXAction, PlayerAction positiveXAction, PlayerAction negativeYAction, PlayerAction positiveYAction)
		{
			PlayerTwoAxisAction playerTwoAxisAction = new PlayerTwoAxisAction(negativeXAction, positiveXAction, negativeYAction, positiveYAction);
			twoAxisActions.Add(playerTwoAxisAction);
			return playerTwoAxisAction;
		}

		public PlayerAction GetPlayerActionByName(string actionName)
		{
			PlayerAction value;
			if (actionsByName.TryGetValue(actionName, out value))
			{
				return value;
			}
			return null;
		}

		internal void Update(ulong updateTick, float deltaTime)
		{
			InputDevice device = Device ?? FindActiveDevice();
			BindingSourceType lastInputType = LastInputType;
			ulong lastInputTypeChangedTick = LastInputTypeChangedTick;
			InputDeviceClass lastDeviceClass = LastDeviceClass;
			InputDeviceStyle lastDeviceStyle = LastDeviceStyle;
			int count = actions.Count;
			for (int i = 0; i < count; i++)
			{
				PlayerAction playerAction = actions[i];
				playerAction.Update(updateTick, deltaTime, device);
				if (playerAction.UpdateTick > UpdateTick)
				{
					UpdateTick = playerAction.UpdateTick;
					activeDevice = playerAction.ActiveDevice;
				}
				if (playerAction.LastInputTypeChangedTick > lastInputTypeChangedTick)
				{
					lastInputType = playerAction.LastInputType;
					lastInputTypeChangedTick = playerAction.LastInputTypeChangedTick;
					lastDeviceClass = playerAction.LastDeviceClass;
					lastDeviceStyle = playerAction.LastDeviceStyle;
				}
			}
			int count2 = oneAxisActions.Count;
			for (int j = 0; j < count2; j++)
			{
				oneAxisActions[j].Update(updateTick, deltaTime);
			}
			int count3 = twoAxisActions.Count;
			for (int k = 0; k < count3; k++)
			{
				twoAxisActions[k].Update(updateTick, deltaTime);
			}
			if (lastInputTypeChangedTick > LastInputTypeChangedTick)
			{
				bool flag = lastInputType != LastInputType;
				LastInputType = lastInputType;
				LastInputTypeChangedTick = lastInputTypeChangedTick;
				LastDeviceClass = lastDeviceClass;
				LastDeviceStyle = lastDeviceStyle;
				if (this.OnLastInputTypeChanged != null && flag)
				{
					this.OnLastInputTypeChanged(lastInputType);
				}
			}
		}

		public void Reset()
		{
			int count = actions.Count;
			for (int i = 0; i < count; i++)
			{
				actions[i].ResetBindings();
			}
		}

		private InputDevice FindActiveDevice()
		{
			bool flag = IncludeDevices.Count > 0;
			bool flag2 = ExcludeDevices.Count > 0;
			if (flag || flag2)
			{
				InputDevice inputDevice = InputDevice.Null;
				int count = InputManager.Devices.Count;
				for (int i = 0; i < count; i++)
				{
					InputDevice inputDevice2 = InputManager.Devices[i];
					if (inputDevice2 != inputDevice && inputDevice2.LastChangedAfter(inputDevice) && (!flag2 || !ExcludeDevices.Contains(inputDevice2)) && (!flag || IncludeDevices.Contains(inputDevice2)))
					{
						inputDevice = inputDevice2;
					}
				}
				return inputDevice;
			}
			return InputManager.ActiveDevice;
		}

		public void ClearInputState()
		{
			int count = actions.Count;
			for (int i = 0; i < count; i++)
			{
				actions[i].ClearInputState();
			}
			int count2 = oneAxisActions.Count;
			for (int j = 0; j < count2; j++)
			{
				oneAxisActions[j].ClearInputState();
			}
			int count3 = twoAxisActions.Count;
			for (int k = 0; k < count3; k++)
			{
				twoAxisActions[k].ClearInputState();
			}
		}

		internal bool HasBinding(BindingSource binding)
		{
			if (binding == null)
			{
				return false;
			}
			int count = actions.Count;
			for (int i = 0; i < count; i++)
			{
				if (actions[i].HasBinding(binding))
				{
					return true;
				}
			}
			return false;
		}

		internal void RemoveBinding(BindingSource binding)
		{
			if (!(binding == null))
			{
				int count = actions.Count;
				for (int i = 0; i < count; i++)
				{
					actions[i].FindAndRemoveBinding(binding);
				}
			}
		}

		public string Save()
		{
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8))
				{
					binaryWriter.Write((byte)66);
					binaryWriter.Write((byte)73);
					binaryWriter.Write((byte)78);
					binaryWriter.Write((byte)68);
					binaryWriter.Write((ushort)2);
					int count = actions.Count;
					binaryWriter.Write(count);
					for (int i = 0; i < count; i++)
					{
						actions[i].Save(binaryWriter);
					}
				}
				return Convert.ToBase64String(memoryStream.ToArray());
			}
		}

		public void Load(string data)
		{
			if (data == null)
			{
				return;
			}
			try
			{
				using (MemoryStream input = new MemoryStream(Convert.FromBase64String(data)))
				{
					using (BinaryReader binaryReader = new BinaryReader(input))
					{
						if (binaryReader.ReadUInt32() != 1145981250)
						{
							throw new Exception("Unknown data format.");
						}
						ushort num = binaryReader.ReadUInt16();
						if (num < 1 || num > 2)
						{
							throw new Exception("Unknown data format version: " + num);
						}
						int num2 = binaryReader.ReadInt32();
						for (int i = 0; i < num2; i++)
						{
							PlayerAction value;
							if (actionsByName.TryGetValue(binaryReader.ReadString(), out value))
							{
								value.Load(binaryReader, num);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogError("Provided state could not be loaded:\n" + ex.Message);
				Reset();
			}
		}




	}
}
