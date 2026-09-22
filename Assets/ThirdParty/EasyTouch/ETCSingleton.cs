using System;
using UnityEngine;
public class ETCSingleton<T> : MonoBehaviour where T : ETCSingleton<T>
{
	private static T m_Instance = null;

    // Teardown must not instantiate a new input manager after the old one died.
    public static T ExistingInstance => m_Instance != null ? m_Instance : UnityEngine.Object.FindObjectOfType<T>();

	public static T instance
	{
		get
		{
			if (m_Instance == null)
			{
				m_Instance = UnityEngine.Object.FindObjectOfType(typeof(T)) as T;
				if (m_Instance == null)
				{
					m_Instance = new GameObject("VirtualControlInput", typeof(T)).GetComponent<T>();
					m_Instance.Init();
				}
			}
			return m_Instance;
		}
	}

	private void Awake()
	{
		if (m_Instance == null)
		{
			m_Instance = this as T;
		}
	}

	public virtual void Init()
	{
	}

	private void OnApplicationQuit()
	{
		m_Instance = null;
	}

	public ETCSingleton()
	{
	}




}
