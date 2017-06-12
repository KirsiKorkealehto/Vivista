﻿using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveFile
{
	public static string GetSaveFileContents(string filename)
	{
		string str;
		using (var fileContents = File.OpenText(Path.Combine(Application.persistentDataPath, filename)))
		{
			try
			{
				str = fileContents.ReadToEnd();
			}
			catch (Exception e)
			{
				Debug.Log("Something went wrong while loading the file.");
				Debug.Log(e.ToString());
				return "";
			}
		}

		return str;
	}

	public static byte[] GetSaveFileContentsBinary(string filename)
	{
		byte[] data;
		using (var fileContents = File.OpenRead(Path.Combine(Application.persistentDataPath, filename)))
		{
			try
			{
				data = new byte[(int)fileContents.Length];
				fileContents.Read(data, 0, (int)fileContents.Length);
			}
			catch (Exception e)
			{
				Debug.Log("Something went wrong while loading the file.");
				Debug.Log(e.ToString());
				return new byte[0];
			}
		}

		return data;
	}

	public class SaveFileData
	{
		public Guid guid;
		public string openVideo;
		public Perspective perspective;
		public List<InteractionpointSerialize> points = new List<InteractionpointSerialize>();
	}

	public static SaveFileData OpenFile(string filename)
	{
		var str = GetSaveFileContents(filename);

		var level = 0;
		var start = 0;
		var count = 0;
		var rising = true;

		var saveFileData = new SaveFileData();

		var result = new ParsedJsonLine();

		result = JsonGetValueFromLine(str, result.endindex);
		saveFileData.guid = new Guid(result.value);

		result = JsonGetValueFromLine(str, result.endindex);
		saveFileData.openVideo = result.value;

		result = JsonGetValueFromLine(str, result.endindex);
		saveFileData.perspective = (Perspective)Enum.Parse(typeof(Perspective), result.value);

		//Note(Simon): Value is only used server side, but we still need to skip over the text in the file.
		result = JsonGetValueFromLine(str, result.endindex);

		var stringObjects = new List<string>();
			
		for(var i = result.endindex; i < str.Length; i++)
		{
			if (str[i] == '{')
			{
				if (level == 0)
				{
					start = i;
				}
				rising = true;
				level++;
			}
			if (str[i] == '}')
			{
				level--;
				rising = false;
			}

			count++;

			if (level == 0 && !rising)
			{
				stringObjects.Add(str.Substring(start, count - 1));
				count = 0;
				rising = true;
			}
			if (level < 0)
			{
				Debug.Log("Corrupted save file. Aborting");
				return null;
			}
		}

		
		saveFileData.points = new List<InteractionpointSerialize>();
		
		foreach (var obj in stringObjects)
		{
			saveFileData.points.Add(JsonUtility.FromJson<InteractionpointSerialize>(obj));
		}

		return saveFileData;
	}
	
	public class ParsedJsonLine{
		public string value;
		public int endindex;
	}

	public static ParsedJsonLine JsonGetValueFromLine(string json, int startIndex)
	{
		var startValue = json.IndexOf(':', startIndex) + 1;
		var endValue = json.IndexOf('\n', startIndex) + 1;
		return new ParsedJsonLine
		{
			value = json.Substring(startValue, (endValue- startValue) - 2),
			endindex = endValue
		};
	}
}
