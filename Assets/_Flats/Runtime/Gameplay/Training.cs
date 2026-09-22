using System;
using System.Collections;
using UnityEngine;
public class Training : MonoBehaviour
{
	public UnityEngine.Object enemy1;

	public UnityEngine.Object enemy2;

	public UnityEngine.Object enemy3;

	public UnityEngine.Object enemy4;

	public static int enemy = 8;

	public AudioClip singleplayerBGM2;

	public Transform spawnPoints;

	private Transform[] spawnPoint;

	private void Awake()
	{
		if (Singleplayer.rule != 3)
		{
			UnityEngine.Object.Destroy(this);
			return;
		}
		Transform transform = GameObject.Find("SpawnPoints").transform;
		UnityEngine.Object.Instantiate(Resources.Load("Flatman"), transform.GetChild(UnityEngine.Random.Range(0, transform.childCount)).position, Quaternion.identity);
	}

	private void Start()
	{
		GameObject gameObject = GameObject.Find("Ambient");
		AudioSource[] components = gameObject.GetComponents<AudioSource>();
		components[1].clip = singleplayerBGM2;
		components[1].Play();
		Array.Resize(ref spawnPoint, spawnPoints.childCount);
		for (int i = 0; i < spawnPoint.Length; i++)
		{
			spawnPoint[i] = spawnPoints.GetChild(i).transform;
		}
		StartCoroutine("ResetEnemies");
	}

	private IEnumerator ResetEnemies()
	{
		enemy = 8;
		int lastPoint = 0;
		int ram = UnityEngine.Random.Range(0, spawnPoint.Length - 1);
		if (ram == lastPoint)
		{
			ram = ((ram != spawnPoint.Length) ? (ram + 1) : 0);
		}
		UnityEngine.Object.Instantiate(enemy1, spawnPoint[ram].position, Quaternion.identity);
		lastPoint = ram;
		yield return new WaitForSeconds(0.5f);
		ram = UnityEngine.Random.Range(0, spawnPoint.Length - 1);
		if (ram == lastPoint)
		{
			ram = ((ram != spawnPoint.Length) ? (ram + 1) : 0);
		}
		UnityEngine.Object.Instantiate(enemy1, spawnPoint[ram].position, Quaternion.identity);
		lastPoint = ram;
		yield return new WaitForSeconds(0.5f);
		ram = UnityEngine.Random.Range(0, spawnPoint.Length - 1);
		if (ram == lastPoint)
		{
			ram = ((ram < spawnPoint.Length - 1) ? (ram + 1) : 0);
		}
		UnityEngine.Object.Instantiate(enemy1, spawnPoint[ram].position, Quaternion.identity);
		lastPoint = ram;
		yield return new WaitForSeconds(0.5f);
		ram = UnityEngine.Random.Range(0, spawnPoint.Length - 1);
		if (ram == lastPoint)
		{
			ram = ((ram < spawnPoint.Length - 1) ? (ram + 1) : 0);
		}
		UnityEngine.Object.Instantiate(enemy2, spawnPoint[ram].position, Quaternion.identity);
		lastPoint = ram;
		yield return new WaitForSeconds(0.5f);
		ram = UnityEngine.Random.Range(0, spawnPoint.Length - 1);
		if (ram == lastPoint)
		{
			ram = ((ram < spawnPoint.Length - 1) ? (ram + 1) : 0);
		}
		UnityEngine.Object.Instantiate(enemy2, spawnPoint[ram].position, Quaternion.identity);
		lastPoint = ram;
		yield return new WaitForSeconds(0.5f);
		ram = UnityEngine.Random.Range(0, spawnPoint.Length - 1);
		if (ram == lastPoint)
		{
			ram = ((ram < spawnPoint.Length - 1) ? (ram + 1) : 0);
		}
		UnityEngine.Object.Instantiate(enemy3, spawnPoint[ram].position, Quaternion.identity);
		lastPoint = ram;
		yield return new WaitForSeconds(0.5f);
		ram = UnityEngine.Random.Range(0, spawnPoint.Length - 1);
		if (ram == lastPoint)
		{
			ram = ((ram < spawnPoint.Length - 1) ? (ram + 1) : 0);
		}
		UnityEngine.Object.Instantiate(enemy3, spawnPoint[ram].position, Quaternion.identity);
		lastPoint = ram;
		yield return new WaitForSeconds(0.5f);
		ram = UnityEngine.Random.Range(0, spawnPoint.Length - 1);
		if (ram == lastPoint)
		{
			ram = ((ram < spawnPoint.Length - 1) ? (ram + 1) : 0);
		}
		UnityEngine.Object.Instantiate(enemy4, spawnPoint[ram].position, Quaternion.identity);
		lastPoint = ram;
		yield return new WaitForSeconds(0.5f);
		while (true)
		{
			if (enemy < 8)
			{
				ram = UnityEngine.Random.Range(0, spawnPoint.Length - 1);
				if (ram == lastPoint)
				{
					ram = ((ram < spawnPoint.Length - 1) ? (ram + 1) : 0);
				}
				UnityEngine.Object original = new UnityEngine.Object();
				switch (UnityEngine.Random.Range(1, 5))
				{
				case 1:
					original = enemy1;
					break;
				case 2:
					original = enemy2;
					break;
				case 3:
					original = enemy3;
					break;
				case 4:
					original = enemy4;
					break;
				}
				UnityEngine.Object.Instantiate(original, spawnPoint[ram].position, Quaternion.identity);
				lastPoint = ram;
				enemy++;
			}
			yield return new WaitForSeconds(1f);
		}
	}

	public Training()
	{
	}




}
