using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Scaffolding;

public class Replayer : MonoBehaviour {

	private Dictionary<string, FieldInfo> _fields;
	private Dictionary<string, MethodInfo> _methods;

	//dictionary to map recordings into, key: <Timestamp, <field,value>>
	private Dictionary<int, Dictionary<string,string>> _recording;
	private Dictionary<int,Dictionary<string,object>> _deserialisedRecording;
	private string _lastRecording;
	private InputManager _inputManager;
	private float _recordingTime;
	public bool Record;

	// Use this for initialization
	void Start () {
		Setup();
		LoadClass();
	}

	private void Setup()
	{
		_fields = new Dictionary<string, FieldInfo>();
		_methods = new Dictionary<string, MethodInfo>();
		_recording = new Dictionary<int, Dictionary<string, string>>();
		
		_inputManager = FindObjectOfType<InputManager>();
		if(_inputManager == null)
		{
			GameObject go = new GameObject();
			_inputManager = go.AddComponent<InputManager>();
			go.name = "InputManager";
		}
	}

	private void LoadClass()
	{
		foreach(FieldInfo f in _inputManager.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
		{
			_fields.Add(f.Name, f);
		}
		
		foreach(MethodInfo f in _inputManager.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
		{
			_methods.Add(f.Name, f);
		}
	}

	// Update is called once per frame
	void Update () {
		if(Record)
		{
			Dictionary<int, InputTracker> trackerLookup = (_fields["_trackerLookup"].GetValue(_inputManager) as Dictionary<int,InputTracker>);

			if(trackerLookup.Count > 0)
			{
				foreach(KeyValuePair<int,InputTracker> kvp in trackerLookup)
				{
					RecordInput(kvp.Value);
				}
			}
		}

		if(Input.GetKeyDown(KeyCode.Space))
		{
			if(Record)
			{
				StopRecording();
			}
			else
			{
				StartRecording();
			}
		}
		if(Input.GetKeyDown(KeyCode.P))
		{
			PlaybackRecording();
		}
	}

	private void StartRecording()
	{
		Record = true;
		_recordingTime = Time.realtimeSinceStartup;
		_recording = new Dictionary<int, Dictionary<string, string>>();
	}

	private void RecordInput(InputTracker tracker)
	{
		Dictionary<string,string> input = new Dictionary<string, string>();

		input.Add("TimeStamp", (Time.realtimeSinceStartup - _recordingTime).ToString());
		_recordingTime = Time.realtimeSinceStartup;
		foreach(FieldInfo f in tracker.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
		{
			if(f.GetValue(tracker) is IList)
			{
				if(f.GetValue(tracker) is List<Camera>)
				{
					string cameras = "";
					foreach (Camera c in (f.GetValue(tracker) as List<Camera>))
					{
						cameras += c.name+",";
					}
					cameras = cameras.Remove(cameras.Length-1,1);
					input.Add(f.Name,cameras);
				}
			}
			else
			{
				System.Object obj = f.GetValue(tracker);
				if(obj != null)
				{
					input.Add(f.Name,f.GetValue(tracker).ToString());
				}
			}
		}

		_recording.Add(_recording.Count, input);
	}

	private void StopRecording()
	{
		Record = false;
		_lastRecording = MiniJSON.Json.Serialize(_recording);
	}

	private Vector3 GetVector3(string rString){
		string[] temp = rString.Substring(1,rString.Length-2).Split(',');
		float x = float.Parse(temp[0]);
		float y = float.Parse(temp[1]);
		float z = float.Parse(temp[2]);
		Vector3 rValue = new Vector3(x,y,z);
		return rValue;
	}

	private Vector2 GetVector2(string rString){
		string[] temp = rString.Substring(1,rString.Length-2).Split(',');
		float x = float.Parse(temp[0]);
		float y = float.Parse(temp[1]);
		Vector2 rValue = new Vector2(x,y);
		return rValue;
	}

	private void PlaybackRecording()
	{
		_deserialisedRecording = new Dictionary<int, Dictionary<string, object>>();

		Dictionary<string,object> dict = MiniJSON.Json.Deserialize(_lastRecording) as Dictionary<string,object>;
		foreach(KeyValuePair<string, object> kvp in dict)
		{
			string index = kvp.Key;

			_deserialisedRecording.Add(System.Convert.ToInt32(index),kvp.Value as Dictionary<string,object>);
		}

		StartCoroutine(Playback());
	}

	IEnumerator Playback()
	{
		int index = 0;

		while(index < _deserialisedRecording.Count-1)
		{
			List<InputTracker> trackers = new List<InputTracker>();

			Dictionary<string,object> input = _deserialisedRecording[index];
			float timeStamp = float.Parse(input["TimeStamp"].ToString());
			//need to get the freshest of fresh.
			Dictionary<int, InputTracker> trackerLookup = (_fields["_trackerLookup"].GetValue(_inputManager) as Dictionary<int,InputTracker>);

			int fingerID = System.Convert.ToInt32(input["_fingerId"]);
			Vector3 position = GetVector3(input["_position"].ToString());
			InputTracker tracker = null;
			if(trackerLookup.ContainsKey(fingerID))
			{
				tracker = trackerLookup[fingerID];
				foreach(FieldInfo info in tracker.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
				{
					if(input.ContainsKey(info.Name))
					{
						System.Type t = info.GetValue(tracker).GetType();
						string s = input[info.Name].ToString();
						if(t == typeof(Vector3))
						{
							info.SetValue(tracker,System.Convert.ChangeType(GetVector3(s),t));
						}
						else if(t == typeof(Vector2))
						{
							info.SetValue(tracker,System.Convert.ChangeType(GetVector2(s),t));
						}
						else if(t == typeof(List<Camera>))
						{
							//need to parse the cameras
						}
						else
						{
							info.SetValue(tracker,System.Convert.ChangeType(s,t));
						}
					}
				}
			}
			else
			{
				object[] parms = new object[2];
				parms[0] = fingerID;
				parms[1] = position;
				_methods["BeginTracking"].Invoke(_inputManager,parms);
			}

			trackerLookup = (_fields["_trackerLookup"].GetValue(_inputManager) as Dictionary<int,InputTracker>);
			tracker = trackerLookup[fingerID];
			trackers.Add(tracker);

			object[] p = new object[1];
			p[0] = trackers;
			_methods["FilterAliveTrackers"].Invoke(_inputManager,p);

			yield return new WaitForSeconds(timeStamp);
			index ++;
		}
	}

}
