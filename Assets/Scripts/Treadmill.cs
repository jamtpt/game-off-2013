﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Treadmill : MonoBehaviour
{
	// Section Algorithm Configurations
	const int MIN_WILDCARD_DISTANCE = 400;
	const int MAX_WILDCARD_DISTANCE = 1000;
	int nextWildcardMarker;
	bool needsWildcard;
	const int MIN_FREEBIE_DISTANCE = 350; // ~5 sections of 70m
	const int MAX_FREEBIE_DISTANCE = 500;
	int nextFreebieMarker;
	public const int HARD_THRESHOLD = 1;
	const int MIN_CHALLENGE_DISTANCE = 250;
	const int MAX_CHALLENGE_DISTANCE = 750;
	int nextChallengeMarker;
	int MAX_WILDCARDS = 9;
	
	const float STARTING_SPEED = 20.0f;
	const float STARTING_ACCEL = 0.005f;
	public float distanceTraveled;
	public float scrollspeed;
	float previousScrollspeed;
	float accelerationPerFrame = 0.005f;
	float prevAccelerationPerFrame;
	public float maxspeed = 50.0f;
	public GameObject emptySection;
	public GameObject tutorialLessonLaser;
	public GameObject tutorialLessonShields;
	public GameObject tutorialLessonSlow;
	public List<GameObject> normalSections;
	public List<GameObject> challengeSections;
	public List<GameObject> freebieSections;
	
	List<GameObject> sectionsInPlay;
	Transform sectionSpawnZone;
	Transform sectionKillZone;
	
	float lerpToSpeed;
	public bool lerping;
	const int lerpSpeed = 5;
	
	// TODO This can be removed before launch, for debugging
	Hashtable sectionCount = new Hashtable();
	
	Status status;
	
	enum Status {
		Tutorial,
		Stopped,
		Started
	}
	
	void Awake ()
	{
		sectionsInPlay = new List<GameObject> ();
		sectionSpawnZone = (Transform)GameObject.Find (ObjectNames.SECTION_SPAWN).transform;
		sectionKillZone = (Transform)GameObject.Find (ObjectNames.SECTION_KILLZONE).transform;
		nextWildcardMarker = GenerateNextMarker (MIN_WILDCARD_DISTANCE, MAX_WILDCARD_DISTANCE);
		nextFreebieMarker = GenerateNextMarker (MIN_FREEBIE_DISTANCE, MAX_FREEBIE_DISTANCE);
		nextChallengeMarker = GenerateNextMarker (MAX_CHALLENGE_DISTANCE, MAX_CHALLENGE_DISTANCE);
		//AddAllSectionsToCounter ();
	}
	
	void Update ()
	{
		if (status == Status.Started) {
			if (!needsWildcard && distanceTraveled >= nextWildcardMarker) {
				SetNeedsWildcard (true);
			}
			
			if (lerping) {
				UpdateLerping ();
			} else {
				scrollspeed = Mathf.Min ((scrollspeed + accelerationPerFrame), maxspeed);
			}
			// Move our treadmill and add up the distance
			float distance = scrollspeed * Time.deltaTime;
			transform.Translate (new Vector3 (0, 0, -distance));
			AddDistanceTraveled (distance);
			// Check if our last section is on screen. If so, spawn another.
			if (GetLastSectionInPlay () == null) {
				if (GameManager.Instance.SAVE_TUTORIAL_COMPLETE) {
					SpawnNextSection ();
				} else {
					SpawnNextLesson ();
				}
			} else if (isSectionOnScreen (GetLastSectionInPlay ())) {
				if (GameManager.Instance.SAVE_TUTORIAL_COMPLETE) {
					SpawnNextSection ();
				} else {
					SpawnNextLesson ();
				}
			}
			// Check if the first section is past the kill line. If so, kill it!
			if (isSectionPastKillZone (sectionsInPlay [0])) {
				KillSection (sectionsInPlay [0]);
			}
		}
	}
	
	/*
	 * Queues or unqueues the spawning of a wildcard, if able.
	 */
	void SetNeedsWildcard (bool wantsToSpawnWildcard) {
		if(wantsToSpawnWildcard && GameManager.Instance.player.WildcardCount < MAX_WILDCARDS )
		{
			needsWildcard = true;
		} else {
			needsWildcard = false;
		}
	}
	
	/*
	 * Return true if tutorial is being shown.
	 */
	public bool IsShowingTutorial ()
	{
		return status == Status.Tutorial;
	}
	
	#region #1 Treadmill Manipulation (Start/Stop/Reset/Slowdown)
	public void ShowTutorial ()
	{
		// Reset the tutorial image
		GameObject.Find (ObjectNames.TUTORIAL_IMAGE).transform.position = new Vector3 (0, 0.1f, 9.0f);
		scrollspeed = 0;
		status = Status.Tutorial;
	}
	
	public void StartScrolling ()
	{
		scrollspeed = STARTING_SPEED;
		status = Status.Started;
	}
	
	public void PauseScrolling ()
	{
		PauseSpeed();
		PauseAcceleration();
		status = Status.Stopped;
	}
	
	/*
	 * Temporarily turn off speed of the treadmill. Call ResumeTreadmill to restore its speed.
	 */
	void PauseSpeed ()
	{
		if (scrollspeed == 0) {
			Debug.LogWarning ("Tried to pause speed when it was already at 0!");
			return;
		}
		previousScrollspeed = scrollspeed;
		scrollspeed = 0;
	}

	/*
	 * Temporarily turn off acceleration of treadmill. Make sure to call
	 * ResumeAcceleration when done!
	 */
	public void PauseAcceleration ()
	{
		if (accelerationPerFrame == 0) {
			Debug.LogWarning ("Tried to pause acceleration when it was already at 0!");
			return;
		}
		prevAccelerationPerFrame = accelerationPerFrame;
		accelerationPerFrame = 0;
	}
	
	/*
	 * Restore the acceleration back to previous value.
	 */
	public void ResumeAcceleration ()
	{
		accelerationPerFrame = prevAccelerationPerFrame;
	}
	
	/*
	 * Cause the treadmill to stop until further notice
	 */
	public void TemporarySlowDown (float amount)
	{
		previousScrollspeed = scrollspeed;
		PauseAcceleration ();
		LerpToSpeed (Mathf.Max (STARTING_SPEED, scrollspeed - amount));
	}
	
	/*
	 * Bring the treadmill back up to it's previous speed and acceleration.
	 */
	public void ResumeTreadmill ()
	{
		LerpToSpeed (previousScrollspeed);
		ResumeAcceleration ();
		status = Status.Started;
	}
	
	/*
	 * Reset treadmill to prepare for a new game. Values should be restored to default
	 * and any sections in play should be destroyed.
	 */
	public void ResetTreadmill ()
	{
		for (int i = sectionsInPlay.Count-1; i >= 0; i--) {
			KillSection (sectionsInPlay[i]);
		}
		scrollspeed = STARTING_SPEED;
		previousScrollspeed = STARTING_SPEED;
		accelerationPerFrame = STARTING_ACCEL;
		lerping = false;
		lerpToSpeed = STARTING_SPEED;
		distanceTraveled = 0.0f;
		SetNeedsWildcard (false);
		
		nextWildcardMarker = GenerateNextMarker (MIN_WILDCARD_DISTANCE, MAX_WILDCARD_DISTANCE);
		nextFreebieMarker = GenerateNextMarker (MIN_FREEBIE_DISTANCE, MAX_FREEBIE_DISTANCE);
		nextChallengeMarker = GenerateNextMarker (MIN_CHALLENGE_DISTANCE, MAX_CHALLENGE_DISTANCE);
	}
	
	/*
	 * Handle the lerping process and turn off lerping when done.
	 */
	void UpdateLerping () {
		scrollspeed = Mathf.Lerp (scrollspeed, lerpToSpeed, lerpSpeed * Time.deltaTime);
		if (Mathf.RoundToInt(scrollspeed) == Mathf.RoundToInt (lerpToSpeed)) {
			lerping = false;
		}
	}
	
	/*
	 * Use this to change the speed of the treadmill with an acceleration or deceleration.
	 */
	void LerpToSpeed (float newSpeed)
	{
		if (!lerping) {
			lerpToSpeed = newSpeed;
			lerping = true;
		}
	}
	
	public void StartBoostSpeed()
	{
		scrollspeed = maxspeed;
	}
	
	public void StopBoostSpeed()
	{
		LerpToSpeed(STARTING_SPEED);
	}
	
	/*
	 * Add the provided distance to our distance stat. Ignore it if
	 * the game is still in tutorial mode.
	 */
	void AddDistanceTraveled (float distance)
	{
		if (GameManager.Instance.SAVE_TUTORIAL_COMPLETE) {
			distanceTraveled += distance;
		}
	}
	
	/*
	 * Return true if the treadmill has passed the distance for hard mode.
	 */
	public bool IsPastHardThreshold ()
	{
		return distanceTraveled > HARD_THRESHOLD;
	}
	#endregion
	
	#region #2 Section Logic
	/*
	 * Check if the provided (but should be the most recently generated) section has past the spawn zone.
	 * If it has past, return true.
	 */
	bool isSectionOnScreen (GameObject backSection)
	{
		Transform backEdge = (Transform)backSection.transform.FindChild (ObjectNames.BACK_EDGE);
		return backEdge.position.z <= sectionSpawnZone.position.z;
	}
	
	/*
	 * Check if the provided (but should be the least recently generated) section has past the kill zone.
	 * Return true if it has. We can compare z since it's locked for the player.
	 */
	bool isSectionPastKillZone (GameObject frontSection)
	{
		Transform backEdge = (Transform)frontSection.transform.FindChild (ObjectNames.BACK_EDGE);
		return backEdge.position.z <= sectionKillZone.position.z;
	}

	/*
	 * Using our level generation algorithm, select and spawn a new section. Destory the
	 * stale section (the section past the player) and assign the section on top of the player
	 * now as stale, to be destroyed next.
	 */
	void SpawnNextSection ()
	{
		// Determine which bucket of sections to draw from
		List<GameObject> sectionBucket = normalSections;

		// Pull from the freebie bucket when nextFreebieMarker has been passed
		if (distanceTraveled >= nextFreebieMarker) {
			sectionBucket = freebieSections;
			nextFreebieMarker = GenerateNextMarker (MIN_FREEBIE_DISTANCE, MAX_FREEBIE_DISTANCE);
			Debug.Log ("Pulling a new freebie section. Next section at: " + nextFreebieMarker);
		}
		
		// Pull from the challenge bucket when nextChallengeMarker has been passed
		// -- takes a lower priority than freebie bucket
		else if (distanceTraveled >= nextChallengeMarker && GameManager.Instance.IsHard ()) {
			// Force wildcard to spawn?
			SetNeedsWildcard (true);
			sectionBucket = challengeSections;
			nextChallengeMarker = GenerateNextMarker (MIN_CHALLENGE_DISTANCE, MAX_CHALLENGE_DISTANCE);
			Debug.Log ("Pulling a challenge section. Next challenge at: " + nextChallengeMarker);
		}
		
		SpawnSection (GetRandomSectionFromBucket (sectionBucket));
	}
	
	/*
	 * Helper method to spawn a provided section at the spawn zone.
	 */
	void SpawnSection (GameObject sectionToSpawn)
	{
		Vector3 rowSpacing = new Vector3 (0, 0, 1);
		GameObject newSection = (GameObject)Instantiate (sectionToSpawn,
				sectionSpawnZone.position + rowSpacing, Quaternion.identity);
		CountSection (newSection.name);
		sectionsInPlay.Add (newSection);
	}
	
	/*
	 * Using the provided list of sections, find one that is compatible and return it.
	 * If none is found, return an empty section.
	 */
	GameObject GetRandomSectionFromBucket (List<GameObject> sectionsToPickFrom)
	{
		// Set up a bag of indexes to draw from for the provided sectionsToPickFrom
		List<int> bagOfIndexes = new List<int> (sectionsToPickFrom.Count);
		for (int i = 0; i < sectionsToPickFrom.Count; i++) {
			bagOfIndexes.Add (i);
		}
		RBRandom.Shuffle<int> (bagOfIndexes);
		// Just take the first you get if it's the first one drawn
		if (GetLastSectionInPlay () == null) {
			return sectionsToPickFrom[bagOfIndexes[0]];
		}
		
		// Iterate through our random bag until we pick a section that is compatible.
		Section sectionToCheck;
		Section lastSection = (Section) GetLastSectionInPlay ().GetComponent<Section> ();
		int compatibleSectionIndex = -1;
		foreach (int index in bagOfIndexes) {
			sectionToCheck = sectionsToPickFrom[index].GetComponent<Section> ();
			if (lastSection.CanBeFollowedBy (sectionToCheck)) {
				compatibleSectionIndex = index;
				break;
			}
		}

		if (compatibleSectionIndex == -1) {
			Debug.LogWarning ("Couldn't find a compatible section in the bucket. This can " +
				"be fixed by adding a section prefab with open exits and entrances.");
			return emptySection;
		}
		return sectionsToPickFrom [compatibleSectionIndex];
	}
	
	/*
	 * Helper method to return the last section on the treadmill.
	 */
	GameObject GetLastSectionInPlay ()
	{
		if (sectionsInPlay.Count == 0) {
			return null;
		}
		return sectionsInPlay [Mathf.Max (sectionsInPlay.Count - 1, 0)];
	}
	
	void CountSection (string name)
	{
		if (sectionCount[name] != null) {
			int oldval = (int) sectionCount[name];
			sectionCount[name] = oldval + 1;
		} else {
			sectionCount.Add (name, 1);
		}
	}
	
	/*
	 * Remove any references to the provided section and destroy it.
	 */
	void KillSection (GameObject sectionToKill)
	{
		sectionsInPlay.Remove (sectionToKill);
		Destroy (sectionToKill);
	}
	
	/*
	 * Calculate where the next distance marker should be.
	 */
	int GenerateNextMarker (int min, int max)
	{
		return (int) distanceTraveled + Random.Range (min, max);
	}
	
	/*
	 * Return whether the treadmill needs a wilcard to be spawned.
	 */
	public bool NeedsWildcard ()
	{
		return needsWildcard;
	}
	
	/*
	 * Called when a wildcard is successfully spawned.
	 */
	public void OnWildcardSpawn ()
	{
		needsWildcard = false;
		nextWildcardMarker = GenerateNextMarker (MIN_WILDCARD_DISTANCE, MAX_WILDCARD_DISTANCE);
		Debug.Log ("Spawned a new wildcard. Next wildcard at: " + nextWildcardMarker);
	}
	
	/*
	 * Spawn the next lesson that the user hasn't seen.
	 */
	void SpawnNextLesson ()
	{
		if (!GameManager.Instance.SAVE_LASER_LESSON_COMPLETE) {
			// Award them half full meters so that they can fill up on the
			// number of pickups we've placed.
			RedPower power = GameManager.Instance.player.redPower;
			power.AddPower(power.maxValue / 2);
			SpawnSection (tutorialLessonLaser);
		} else if (!GameManager.Instance.SAVE_SHIELDS_LESSON_COMPLETE) {
			GreenPower power = GameManager.Instance.player.greenPower;
			power.AddPower(power.maxValue / 2);
			SpawnSection (tutorialLessonShields);
		} else if (!GameManager.Instance.SAVE_SLOW_LESSON_COMPLETE) {
			BluePower power = GameManager.Instance.player.bluePower;
			power.AddPower(power.maxValue / 2);
			SpawnSection (tutorialLessonSlow);
		}
	}
	#endregion
	
	/*
	 * When closing the program, report back which sections spawned and how many times.
	 */
	void OnDestroy ()
	{
		//PrintSectionCount ();
	}

	/*
	 * Print all keys and values for the section count.
	 */
	void PrintSectionCount ()
	{
		// Print out the data on each section
		foreach (string key in sectionCount.Keys) {
			Debug.Log (key + ": " + sectionCount[key]);
		}
	}
	
	/*
	 * Make sure we get 0 added for all of our possible sections to see
	 * which sections are never instantiated.
	 */
	void AddAllSectionsToCounter ()
	{
		foreach (GameObject obj in normalSections) {
			sectionCount.Add (obj.name, 0);
		}
		foreach (GameObject obj in challengeSections) {
			sectionCount.Add (obj.name, 0);
		}
		foreach (GameObject obj in freebieSections) {
			sectionCount.Add (obj.name, 0);
		}
	}
}
