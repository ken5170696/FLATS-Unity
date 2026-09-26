using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class ControllerMappingEditor : MonoBehaviour
{
	public AudioClip beep;

	public Image controllerImage;

	public Text controllerName;

	public Text actionText;

	public Text defaultText;

	public Text inputText;

	public float currentWait;

	private float waitTime;

	public GameObject startButton;

	public GameObject deleteButton;

	public bool next;

	public bool finished;

	public int step;

	public List<Sprite> currentImage;

	public List<string> custom;

	private string[] currentAction;

	private string[] currentDefault;

	private StandaloneInputModule standaloneModule;

	private InControlInputModule inControlModule;

	private void Awake()
	{
		Input.ResetInputAxes();
		standaloneModule = EventSystem.current.gameObject.GetComponent<StandaloneInputModule>();
		inControlModule = EventSystem.current.gameObject.GetComponent<InControlInputModule>();
	}

	private void Start()
	{
		currentAction[0] = " ";
		currentDefault[0] = " ";
		currentAction[1] = "Input [Jump/Run] command.";
		currentDefault[1] = "Default is [A]";
		currentAction[2] = "Input [Grenade/Pick] command.";
		currentDefault[2] = "Default is [B]";
		currentAction[3] = "Input [Reload] command.";
		currentDefault[3] = "Default is [X]";
		currentAction[4] = "Input [Change Weapons] command.";
		currentDefault[4] = "Default is [Y]";
		currentAction[5] = "Input [Aim/Zoom] command.";
		currentDefault[5] = "Default is [Left Trigger or Left Bumper]";
		currentAction[6] = "Input [Shoot] command.";
		currentDefault[6] = "Default is [Right Trigger or Right Bumper]";
		currentAction[7] = "Finished Mapping.";
		currentDefault[7] = "";
	}

	private void OnDisable()
	{
		step = 0;
		finished = false;
	}

	private void OnEnable()
	{
		if (!Menu.VRmode)
		{
			EventSystem.current.SetSelectedGameObject(startButton);
		}
	}

	private void Update()
	{
		if (next)
		{
			actionText.text = "Wait...";
			defaultText.text = "Release the button.";
		}
		else
		{
			if (!finished)
			{
				actionText.text = currentAction[step];
				defaultText.text = currentDefault[step];
			}
			if (step > 0 && step < 7)
			{
				inputText.text = currentWait.ToString("#");
				currentWait -= Time.unscaledDeltaTime;
				for (int i = 0; i < 20; i++)
				{
					if (Mathf.Abs(Input.GetAxis("joystick 1 analog " + i)) > 0.8f && step >= 5)
					{
						string text = "analog " + i;
						custom[step - 1] = text;
						inputText.text = UppercaseFirst(text);
						Input.ResetInputAxes();
						next = true;
					}
				}
				for (int j = 0; j < 20; j++)
				{
					if (Input.GetKeyDown("joystick 1 button " + j) && step > 0)
					{
						string text2 = "button " + j;
						custom[step - 1] = text2;
						inputText.text = UppercaseFirst(text2);
						Input.ResetInputAxes();
						next = true;
					}
				}
				if (currentWait <= 0f)
				{
					custom[step - 1] = "button null";
					inputText.text = "None";
					Input.ResetInputAxes();
					next = true;
				}
			}
			else if (step == 7)
			{
				startButton.SetActive(true);
				deleteButton.SetActive(true);
				if (custom.Count != 0)
				{
					bool flag = false;
					for (int k = 0; k < custom.Count; k++)
					{
						for (int l = 0; l < custom.Count; l++)
						{
							if (custom[k] == custom[l] && k != l)
							{
								flag = true;
							}
						}
					}
					if (!flag && custom[0] != "button null" && custom[1] != "button null" && custom[2] != "button null" && custom[3] != "button null" && custom[4] != "button null" && custom[5] != "button null")
					{
						FlatsPreferences.SetString("controllermapping", Input.GetJoystickNames()[0] + "$" + custom[0] + "$" + custom[1] + "$" + custom[2] + "$" + custom[3] + "$" + custom[4] + "$" + custom[5]);
						FlatsPreferences.Save();
						string text3 = FlatsPreferences.GetString("controllermapping");
						string[] array = text3.Split(new string[1] { "$" }, StringSplitOptions.None);
						if (Input.GetJoystickNames()[0] == array[0])
						{
							Menu.customControl["ControllerName"] = array[0];
							Menu.customControl["Jump"] = "joystick 1 " + array[1];
							Menu.customControl["Pick"] = "joystick 1 " + array[2];
							Menu.customControl["Reload"] = "joystick 1 " + array[3];
							Menu.customControl["Change"] = "joystick 1 " + array[4];
							Menu.customControl["Zoom"] = "joystick 1 " + array[5];
							Menu.customControl["Fire"] = "joystick 1 " + array[6];
							Menu.customControlEnabled = true;
							standaloneModule.submitButton = "Submit";
							standaloneModule.cancelButton = "Cancel";
							standaloneModule.enabled = true;
							inControlModule.enabled = false;
						}
					}
					else
					{
						base.transform.root.GetChild(0).GetComponent<Menu>().ShowConfirm("Incorrect input", "Custom mapping failed.", null, "OK", null);
					}
				}
				else
				{
					base.transform.root.GetChild(0).GetComponent<Menu>().ShowConfirm("Incorrect input", "Custom mapping failed.", null, "OK", null);
				}
				base.transform.parent.parent.parent.GetChild(0).gameObject.SetActive(true);
				step = 0;
				next = false;
				finished = true;
				waitTime = 10f;
			}
		}
		if (Input.GetJoystickNames().Length == 0)
		{
			controllerName.text = "No controller detected.";
		}
		else
		{
			controllerName.text = Input.GetJoystickNames()[0];
		}
	}

	public void Command(string command)
	{
		if (command == "enable")
		{
			if (Input.GetJoystickNames().Length == 0)
			{
				base.transform.parent.parent.parent.GetComponent<Menu>().ShowConfirm("Connect a controller", "You must connect a controller to make its custom mapping.", null, "OK", null);
				return;
			}
			base.transform.parent.parent.parent.GetChild(0).gameObject.SetActive(false);
			currentWait = waitTime;
			custom = new List<string>(new string[6]);
			startButton.SetActive(false);
			deleteButton.SetActive(false);
			EventSystem.current.SetSelectedGameObject(null);
			step = 0;
			next = true;
			finished = false;
			StartCoroutine("StartMapping");
		}
		else if (command == "disable")
		{
			custom = new List<string>(new string[6]);
			if (FlatsPreferences.HasKey("controllermapping"))
			{
				FlatsPreferences.DeleteKey("controllermapping");
				FlatsPreferences.Save();
				Menu.customControlEnabled = false;
				standaloneModule.submitButton = "Submit";
				standaloneModule.cancelButton = "Cancel";
				standaloneModule.enabled = false;
				inControlModule.enabled = true;
				base.transform.parent.parent.parent.GetComponent<Menu>().ShowConfirm("Deleted your custom mapping.", "Your custom controller mapping has been deleted.", null, "OK", null);
			}
			else
			{
				base.transform.parent.parent.parent.GetComponent<Menu>().ShowConfirm("No custom mapping.", "Your custom controller mapping does not exist.", null, "OK", null);
			}
		}
	}

	private IEnumerator StartMapping()
	{
		while (true)
		{
			if (next)
			{
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(1f));
				inputText.text = "";
				Input.ResetInputAxes();
				step++;
				currentWait = waitTime;
				next = false;
				if (step == 7)
				{
					break;
				}
			}
			yield return new WaitForSeconds(0f);
		}
	}

	private static string UppercaseFirst(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return string.Empty;
		}
		return char.ToUpper(s[0]) + s.Substring(1);
	}

	public ControllerMappingEditor()
	{
		currentWait = 10f;
		waitTime = 10f;
		currentAction = new string[8];
		currentDefault = new string[8];

	}




}
