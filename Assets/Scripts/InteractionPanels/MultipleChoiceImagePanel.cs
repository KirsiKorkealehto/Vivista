﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultipleChoiceImagePanel : MonoBehaviour
{
	public Text question;
	public List<string> answers;
	public int correct;
	public RectTransform imageList;
	public List<MultipleChoiceImageEntry> entries;

	public GameObject multipleChoiceImageEntryPrefab;

	public void OnEnable()
	{
		for (int i = 0; i < entries.Count; i++)
		{
			StartCoroutine(entries[i].SetUrl(answers[i], true));
		}
	}

	public void Init(string newQuestion, List<string> newAnswers, int newCorrect)
	{
		question.text = newQuestion;
		correct = newCorrect;
		answers = newAnswers;

		for (int i = 0; i < answers.Count; i++)
		{
			var go = Instantiate(multipleChoiceImageEntryPrefab, imageList);
			var entry = go.GetComponent<MultipleChoiceImageEntry>();
			entry.toggle.interactable = false;

			entry.toggle.SetIsOnWithoutNotify(i == correct);
			entries.Add(entry);
		}

		Update();
	}

	void Update()
	{
		if (SceneManager.GetActiveScene().name == "Editor")
		{
			GetComponent<Canvas>().transform.rotation = Camera.main.transform.rotation;
		}
	}

	public void Move(Vector3 position)
	{
		GetComponent<Canvas>().GetComponent<RectTransform>().position = position;
	}
}
