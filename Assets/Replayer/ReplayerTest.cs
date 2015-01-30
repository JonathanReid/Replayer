using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Scaffolding;

public class ReplayerTest : MonoBehaviour {

	private List<Vector3> _points;

	// Use this for initialization
	void Awake () {
		_points = new List<Vector3>();
		InputManager im = gameObject.GetReference(typeof(InputManager)) as InputManager;
		im.EventDragged += HandleEventDragged;
	}

	void HandleEventDragged (InputTracker tracker)
	{
		Vector3 pos = Camera.main.ScreenToWorldPoint(tracker.Position);
		pos.z = 0;
		_points.Add(pos);
	}

	void Update()
	{
		if(Input.GetKeyDown(KeyCode.E))
		{
			_points = new List<Vector3>();
		}
	}

	void OnDrawGizmos()
	{
		if(_points != null)
		{
			int i = 1, l = _points.Count-1;
			for(;i<l;++i)
			{
				Gizmos.DrawLine(_points[i-1],_points[i]);
			}
		}
	}
}
