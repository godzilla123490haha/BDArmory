//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18449
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using System;
using System.Collections;
using UnityEngine;
namespace BahaTurret
{
	public class ModuleTargetingCamera : PartModule
	{
		[KSPField]
		public string cameraTransformName;
		public Transform cameraParentTransform;

		[KSPField]
		public string eyeHolderTransformName;
		Transform eyeHolderTransform;

		[KSPField]
		public float maxRayDistance = 15500;

		[KSPField]
		public float gimbalLimit = 120;
		public bool gimbalLimitReached = false;

		[KSPField(isPersistant = true)]
		public bool cameraEnabled = false;

		float fov
		{
			get
			{
				return zoomFovs[currentFovIndex];
			}
		}

		[KSPField]
		public string zoomFOVs = "40,15,3,1";
		float[] zoomFovs;// = new float[]{40,15,3,1f};

		[KSPField(isPersistant = true)]
		public int currentFovIndex = 0;

		[KSPField(isPersistant = true)]
		public bool slaveTurrets = false;

		[KSPField(isPersistant = true)]
		public bool CoMLock = false;

		public bool radarLock = false;

		float controlPanelHeight = 84;//80

		[KSPField(isPersistant = true)]
		public bool groundStabilized = false;
		/// <summary>
		/// Point on surface that camera is focused and stabilized on.
		/// </summary>
		public Vector3 groundTargetPosition;

		[KSPField(isPersistant = true)]
		public double savedLat;

		[KSPField(isPersistant = true)]
		public double savedLong;

		[KSPField(isPersistant = true)]
		public double savedAlt;

		public Vector3 bodyRelativeGTP
		{
			get
			{
				return new Vector3d(savedLat, savedLong, savedAlt);
			}

			set
			{
				savedLat = value.x;
				savedLong = value.y;
				savedAlt = value.z;
			}
		}

		bool resetting = false;


		public bool surfaceDetected = false;
		/// <summary>
		/// Point where camera is focused, regardless of whether surface is detected or not.
		/// </summary>
		public Vector3 targetPointPosition; 

		[KSPField(isPersistant = true)]
		public bool nvMode = false;

		//GUI
		public static ModuleTargetingCamera activeCam;
		public static bool camRectInitialized = false;
		public static bool windowIsOpen = false;
		public static Rect camWindowRect;
		float camImageSize = 360;
		bool resizing = false;
		

		Texture2D riTex;
		Texture2D rollIndicatorTexture
		{
			get
			{
				if(!riTex)
				{
					riTex = GameDatabase.Instance.GetTexture("BDArmory/Textures/rollIndicator", false);
				}
				return riTex;
			}
		}

		Texture2D rrTex;
		Texture2D rollReferenceTexture
		{
			get
			{
				if(!rrTex)
				{
					rrTex = GameDatabase.Instance.GetTexture("BDArmory/Textures/rollReference", false);
				}
				return rrTex;
			}
		}

		private MissileFire wpmr;
		public MissileFire weaponManager
		{
			get
			{
				if(wpmr == null || wpmr.vessel!=vessel)
				{
					wpmr = null;
					foreach(var mf in vessel.FindPartModulesImplementing<MissileFire>())
					{
						wpmr = mf;
					}
				}

				return wpmr;
			}
		}


		[KSPEvent(guiName = "Enable", guiActive = true, guiActiveEditor = false)]
		public void EnableButton()
		{
			EnableCamera();
		}

		[KSPAction("Enable")]
		public void AGEnable(KSPActionParam param)
		{
			EnableCamera();
		}

		public void ToggleCamera()
		{
			if(cameraEnabled)
			{
				DisableCamera();
			}
			else
			{
				EnableCamera();
			}
		}

		public void EnableCamera()
		{
			if(!TargetingCamera.Instance)
			{
				Debug.Log ("Tried to enable targeting camera, but camera instance is null.");
				return;
			}
			if(vessel.isActiveVessel)
			{
				activeCam = this;
				windowIsOpen = true;
				TargetingCamera.Instance.EnableCamera(cameraParentTransform);
				TargetingCamera.Instance.nvMode = nvMode;
				TargetingCamera.Instance.SetFOV(fov);
				RefreshWindowSize();
			}

			cameraEnabled = true;

			if(weaponManager)
			{
				weaponManager.mainTGP = this;
			}

			BDATargetManager.RegisterLaserPoint(this);
		}

		public void DisableCamera()
		{
			cameraEnabled = false;
			groundStabilized = false;
			slaveTurrets = false;
			//StopResetting();

			if(!TargetingCamera.Instance)
			{
				Debug.Log ("Tried to disable targeting camera, but camera instance is null.");
				return;
			}
		
			if(vessel.isActiveVessel)
			{
				TargetingCamera.Instance.DisableCamera();
				if(ModuleTargetingCamera.activeCam == this)
				{
					ModuleTargetingCamera.activeCam = FindNextActiveCamera();
					if(!ModuleTargetingCamera.activeCam)
					{
						windowIsOpen = false;
					}
				}
				else
				{
					windowIsOpen = false;
				}



			}
			BDATargetManager.ActiveLasers.Remove(this);

			if(weaponManager && weaponManager.mainTGP == this)
			{
				weaponManager.mainTGP = FindNextActiveCamera();
			}
		}

		ModuleTargetingCamera FindNextActiveCamera()
		{
			foreach(var mtc in vessel.FindPartModulesImplementing<ModuleTargetingCamera>())
			{
				if(mtc.cameraEnabled)
				{
					mtc.EnableCamera();
					return mtc;
				}
			}

			return null;
		}

		public override void OnAwake ()
		{
			base.OnAwake ();
		
			if(HighLogic.LoadedSceneIsFlight)
			{
				if(!TargetingCamera.Instance)
				{
					(new GameObject("TargetingCameraObject")).AddComponent<TargetingCamera>();
				}
			}
		}



		public override void OnStart (StartState state)
		{
			base.OnStart (state);

			if(HighLogic.LoadedSceneIsFlight)
			{
				//GUI setup
				if(!camRectInitialized)
				{
					float windowWidth = camImageSize+16;
					float windowHeight = camImageSize+8+controlPanelHeight;
				
					camWindowRect = new Rect(Screen.width-windowWidth, Screen.height-windowHeight, windowWidth, windowHeight);
					camRectInitialized = true;
				}

				cameraParentTransform = part.FindModelTransform(cameraTransformName);

				eyeHolderTransform = part.FindModelTransform(eyeHolderTransformName);



				ParseFovs();
				//fov = zoomFovs[currentFovIndex];

				//part.OnJustAboutToBeDestroyed += DisableCamera;



				if(cameraEnabled)
				{
					DelayedEnable();
				}

				foreach(var wm in vessel.FindPartModulesImplementing<MissileFire>())
				{
					wm.targetingPods.Add(this);
				}
			}
		}

		public void DelayedEnable()
		{
			StartCoroutine(DelayedEnableRoutine());
		}

		bool delayedEnabling = false;
		IEnumerator DelayedEnableRoutine()
		{
			if(delayedEnabling) yield break;
			delayedEnabling = true;

			Vector3d savedGTP = bodyRelativeGTP;
			Debug.Log("saved gtp: " + Misc.FormattedGeoPos(savedGTP, true));

			while(TargetingCamera.Instance == null)
			{
				yield return null;
			}
			while(!FlightGlobals.ready)
			{
				yield return null;
			}
			while(FlightCamera.fetch == null)
			{
				yield return null;
			}
			while(FlightCamera.fetch.mainCamera == null)
			{
				yield return null;
			}
			while(vessel.packed)
			{
				yield return null;
			}

			while(vessel.mainBody == null)
			{
				yield return null;
			}

			EnableCamera();
			if(groundStabilized)
			{
				Debug.Log("Camera delayed enabled");
				groundTargetPosition = VectorUtils.GetWorldSurfacePostion(savedGTP, vessel.mainBody);// vessel.mainBody.GetWorldSurfacePosition(bodyRelativeGTP.x, bodyRelativeGTP.y, bodyRelativeGTP.z);
				Vector3 lookVector = groundTargetPosition-cameraParentTransform.position;
				cameraParentTransform.rotation = Quaternion.LookRotation(lookVector, VectorUtils.GetUpDirection(cameraParentTransform.transform.position));
				GroundStabilize();
			}
			delayedEnabling = false;
		}


		void Update()
		{
			if(HighLogic.LoadedSceneIsFlight)
			{
				if(cameraEnabled && !vessel.IsControllable)
				{
					DisableCamera();
				}

				if(cameraEnabled && TargetingCamera.ReadyForUse && vessel.IsControllable)
				{
					if(delayedEnabling) return;


					if(!TargetingCamera.Instance || FlightGlobals.currentMainBody == null)
					{
						return;
					}

					if(activeCam == this)
					{
						if(zoomFovs!=null)
						{
							TargetingCamera.Instance.SetFOV(fov);
						}
					}

					if(radarLock)
					{
						if(weaponManager && weaponManager.radar && weaponManager.radar.locked)
						{
							Vector3 radarTargetPos = weaponManager.radar.lockedTarget.predictedPosition + (weaponManager.radar.lockedTarget.velocity*Time.fixedDeltaTime);
							Quaternion lookRotation = Quaternion.LookRotation(radarTargetPos-cameraParentTransform.position, VectorUtils.GetUpDirection(cameraParentTransform.position));
							if(Vector3.Angle(radarTargetPos - cameraParentTransform.position, cameraParentTransform.forward) < 1.5f)
							{
								cameraParentTransform.rotation = lookRotation;
								GroundStabilize();
							}
							else
							{
								if(groundStabilized)
								{
									ClearTarget();
								}
								cameraParentTransform.rotation = Quaternion.RotateTowards(cameraParentTransform.rotation, lookRotation, 80*Time.fixedDeltaTime);
							}
						}
						else
						{
							radarLock = false;
						}
					}

					if(groundStabilized)
					{
						groundTargetPosition = VectorUtils.GetWorldSurfacePostion(bodyRelativeGTP, vessel.mainBody);//vessel.mainBody.GetWorldSurfacePosition(bodyRelativeGTP.x, bodyRelativeGTP.y, bodyRelativeGTP.z);
						Vector3 lookVector = groundTargetPosition-cameraParentTransform.position;
						cameraParentTransform.rotation = Quaternion.LookRotation(lookVector);
					}

					Vector3 lookDirection = cameraParentTransform.forward;
					if(Vector3.Angle(lookDirection, cameraParentTransform.parent.forward) > gimbalLimit)
					{
						lookDirection = Vector3.RotateTowards(cameraParentTransform.transform.parent.forward, lookDirection, gimbalLimit*Mathf.Deg2Rad, 0);
						gimbalLimitReached = true;
					}
					else
					{
						gimbalLimitReached = false;
					}

					cameraParentTransform.rotation = Quaternion.LookRotation(lookDirection, VectorUtils.GetUpDirection(transform.position));
					
					if(eyeHolderTransform)
					{
						Vector3 projectedForward = Vector3.ProjectOnPlane(cameraParentTransform.forward, eyeHolderTransform.parent.up);
						if(projectedForward!=Vector3.zero)
						{
							eyeHolderTransform.rotation = Quaternion.LookRotation(projectedForward, eyeHolderTransform.parent.up);
						}
					}
				
				}

			}
		}

		void FixedUpdate()
		{
			if(HighLogic.LoadedSceneIsFlight)
			{
				if(delayedEnabling) return;

				if(cameraEnabled)
				{
					GetHitPoint();
				}
			}
		}
		




		void OnGUI()
		{
			if (HighLogic.LoadedSceneIsFlight && !MapView.MapIsEnabled && BDArmorySettings.GAME_UI_ENABLED && !delayedEnabling) 
			{
				if (cameraEnabled && vessel.isActiveVessel && FlightGlobals.ready) 
				{
					//window
					if (activeCam == this && TargetingCamera.ReadyForUse) 
					{
						camWindowRect = GUI.Window (125452, camWindowRect, CamWindow, string.Empty, HighLogic.Skin.window);
						BDGUIUtils.UseMouseEventInRect(camWindowRect);
					}

					//locked target icon
					if(groundStabilized)
					{
						BDGUIUtils.DrawTextureOnWorldPos(groundTargetPosition, BDArmorySettings.Instance.greenPointCircleTexture, new Vector3(20, 20), 0);
					}
					else
					{
						BDGUIUtils.DrawTextureOnWorldPos(targetPointPosition, BDArmorySettings.Instance.greenCircleTexture, new Vector3(18, 18), 0);
					}
				}
			}



		}

		void CamWindow(int windowID)
		{
			if(!TargetingCamera.Instance)
			{
				return;
			}

			windowIsOpen = true;

			GUI.DragWindow(new Rect(0,0,camImageSize+16, camImageSize+8));

			Rect imageRect = new Rect(8,20,camImageSize,camImageSize);
			GUI.DrawTexture(imageRect, TargetingCamera.Instance.targetCamRenderTexture, ScaleMode.StretchToFill, false);
			GUI.DrawTexture(imageRect, TargetingCamera.Instance.ReticleTexture, ScaleMode.StretchToFill, true);

			float controlsStartY = 24 + camImageSize + 4;

			//slew buttons
			float slewStartX = 8;
			float slewSize = (controlPanelHeight/3) - 8;
			Rect slewUpRect = new Rect(slewStartX + slewSize, controlsStartY, slewSize, slewSize);
			Rect slewDownRect = new Rect(slewStartX + slewSize, controlsStartY + (2*slewSize), slewSize, slewSize);
			Rect slewLeftRect = new Rect(slewStartX, controlsStartY + slewSize, slewSize, slewSize);
			Rect slewRightRect = new Rect(slewStartX + (2*slewSize), controlsStartY+slewSize, slewSize, slewSize);
			if(GUI.RepeatButton(slewUpRect, "^", HighLogic.Skin.button))
			{
				SlewCamera(Vector3.up);
			}
			if(GUI.RepeatButton(slewDownRect, "v", HighLogic.Skin.button))
			{
				SlewCamera(Vector3.down);
			}
			if(GUI.RepeatButton(slewLeftRect, "<", HighLogic.Skin.button))
			{
				SlewCamera(Vector3.left);
			}
			if(GUI.RepeatButton(slewRightRect, ">", HighLogic.Skin.button))
			{
				SlewCamera(Vector3.right);
			}

			//zoom buttons
			float zoomStartX = 8 + (3*slewSize) + 4;
			Rect zoomInRect = new Rect(zoomStartX, controlsStartY, 3*slewSize, slewSize);
			Rect zoomOutRect = new Rect(zoomStartX, controlsStartY + (2*slewSize), 3*slewSize, slewSize);
			GUIStyle disabledStyle = new GUIStyle();
			disabledStyle.alignment = TextAnchor.MiddleCenter;
			disabledStyle.normal.textColor = Color.white;
			if(currentFovIndex < zoomFovs.Length-1)
			{
				if(GUI.Button(zoomInRect, "In", HighLogic.Skin.button))
				{
					ZoomIn();
				}
			}
			else
			{
				GUI.Label(zoomInRect, "(In)", disabledStyle);
			}
			if(currentFovIndex > 0)
			{
				if(GUI.Button(zoomOutRect, "Out", HighLogic.Skin.button))
				{
					ZoomOut();
				}
			}
			else
			{
				GUI.Label(zoomOutRect, "(Out)", disabledStyle);
			}
			Rect zoomInfoRect = new Rect(zoomStartX, controlsStartY + slewSize, 3*slewSize, slewSize);
			GUIStyle zoomInfoStyle = new GUIStyle(HighLogic.Skin.box);
			zoomInfoStyle.fontSize = 12;
			zoomInfoStyle.wordWrap = false;
			GUI.Label(zoomInfoRect, "Zoom "+(currentFovIndex+1).ToString(), zoomInfoStyle);

			GUIStyle dataStyle = new GUIStyle();
			dataStyle.alignment = TextAnchor.MiddleCenter;
			dataStyle.normal.textColor = Color.white;

			//groundStablize button
			float stabilStartX = zoomStartX + zoomInRect.width + 4;
			Rect stabilizeRect = new Rect(stabilStartX, controlsStartY, 3*slewSize, 3*slewSize);
			if(!groundStabilized)
			{
				if(GUI.Button(stabilizeRect, "Lock\nTarget", HighLogic.Skin.button))
				{
					GroundStabilize();
				}
			}
			else
			{
				if(GUI.Button(new Rect(stabilizeRect.x,stabilizeRect.y,stabilizeRect.width,stabilizeRect.height/2), "Unlock", HighLogic.Skin.button))
				{
					ClearTarget();
				}
				if(weaponManager)
				{
					GUIStyle gpsStyle = new GUIStyle(HighLogic.Skin.button);
					gpsStyle.fontSize = 10;
					if(GUI.Button(new Rect(stabilizeRect.x, stabilizeRect.y + (stabilizeRect.height / 2), stabilizeRect.width, stabilizeRect.height / 2), "Send GPS", gpsStyle))
					{
						BDATargetManager.GPSTargets[BDATargetManager.BoolToTeam(weaponManager.team)].Add(new GPSTargetInfo(bodyRelativeGTP, "Target"));
					}
				}

				if(!gimbalLimitReached)
				{
					//open square
					float oSqrSize = (24f/512f) * camImageSize;
					Rect oSqrRect = new Rect(imageRect.x + (camImageSize/2) - (oSqrSize/2), imageRect.y + (camImageSize/2) - (oSqrSize/2), oSqrSize, oSqrSize);
					GUI.DrawTexture(oSqrRect, BDArmorySettings.Instance.openWhiteSquareTexture, ScaleMode.StretchToFill, true);
				}

				//geo data
				Rect geoRect = new Rect(imageRect.x, (camImageSize * 0.94f), camImageSize, 14);
				string geoLabel = Misc.FormattedGeoPos(bodyRelativeGTP,false);
				GUI.Label(geoRect, geoLabel, dataStyle);

				//target data
				dataStyle.fontSize = 16;
				float dataStartX = stabilStartX + stabilizeRect.width + 8;
				Rect targetRangeRect = new Rect(imageRect.x,(camImageSize * 0.94f) - 18, camImageSize, 18);
				string rangeString = "Range: "+Vector3.Distance(groundTargetPosition, transform.position).ToString("0.0")+"m";
				GUI.Label(targetRangeRect,rangeString, dataStyle);

				//laser ranging indicator
				dataStyle.fontSize = 18;
				string lrLabel = surfaceDetected ? "LR" : "NO LR";
				Rect lrRect = new Rect(imageRect.x, imageRect.y+(camImageSize * 0.65f), camImageSize, 20);
				GUI.Label(lrRect, lrLabel, dataStyle);

				//azimuth and elevation indicator //UNFINISHED
				/*
				Vector2 azielPos = TargetAzimuthElevationScreenPos(imageRect, groundTargetPosition, 4);
				Rect azielRect = new Rect(azielPos.x, azielPos.y, 4, 4);
				GUI.DrawTexture(azielRect, BDArmorySettings.Instance.whiteSquareTexture, ScaleMode.StretchToFill, true);
				*/
			}





			//gimbal limit
			dataStyle.fontSize = 24;
			if(gimbalLimitReached)
			{
				Rect gLimRect = new Rect(imageRect.x, imageRect.y+(camImageSize * 0.15f), camImageSize, 28);
				GUI.Label(gLimRect, "GIMBAL LIMIT", dataStyle);
			}


			//reset button
			float resetStartX = stabilStartX + stabilizeRect.width + 4;
			Rect resetRect = new Rect(resetStartX, controlsStartY + (2*slewSize), 3*slewSize, slewSize-1);
			if(GUI.Button(resetRect, "Reset", HighLogic.Skin.button))
			{
				if(!resetting)
				{
					StartCoroutine("ResetCamera");
				}
			}


			//CoM lock
			Rect comLockRect = new Rect(resetRect.x, controlsStartY, 3*slewSize, slewSize-1);
			GUIStyle comStyle = new GUIStyle(CoMLock ? HighLogic.Skin.box : HighLogic.Skin.button);
			comStyle.fontSize = 10;
			comStyle.wordWrap = false;
			if(GUI.Button(comLockRect, "CoM Track",comStyle))
			{
				CoMLock = !CoMLock;
			}


			//radar slave
			Rect radarSlaveRect = new Rect(comLockRect.x + comLockRect.width + 4, comLockRect.y, 3*slewSize, slewSize-1);
			GUIStyle radarSlaveStyle = radarLock ? HighLogic.Skin.box : HighLogic.Skin.button;
			if(GUI.Button(radarSlaveRect, "Radar", radarSlaveStyle))
			{
				radarLock = !radarLock;
			}

			//slave turrets button
			Rect slaveRect = new Rect(resetStartX, controlsStartY + slewSize, (3*slewSize), slewSize-1);
			if(!slaveTurrets)
			{
				if(GUI.Button(slaveRect, "Turrets", HighLogic.Skin.button))
				{
					SlaveTurrets ();
				}
			}
			else
			{
				if(GUI.Button(slaveRect, "Turrets", HighLogic.Skin.box))
				{
					UnslaveTurrets ();
				}
			}

			//point to gps button
			Rect toGpsRect = new Rect(resetRect.x + slaveRect.width + 4, slaveRect.y, 3*slewSize, slewSize-1);
			if(GUI.Button(toGpsRect, "To GPS", HighLogic.Skin.button))
			{
				PointToGPSTarget();
			}


			//nv button
			float nvStartX = resetStartX + resetRect.width + 4;
			Rect nvRect = new Rect(nvStartX, resetRect.y, 3*slewSize, slewSize-1);
			string nvLabel = nvMode ? "NV Off" : "NV On";
			GUIStyle nvStyle = nvMode ? HighLogic.Skin.box : HighLogic.Skin.button;
			if(GUI.Button(nvRect, nvLabel, nvStyle))
			{
				nvMode = !nvMode;
				TargetingCamera.Instance.nvMode = nvMode;
			}

			//off button
			float offStartX = nvStartX + nvRect.width + 4;
			Rect offRect = new Rect(offStartX, controlsStartY, slewSize*1.5f, 3*slewSize);
			if(GUI.Button(offRect, "O\nF\nF", HighLogic.Skin.button))
			{
				DisableCamera();
			}


			float indicatorSize = Mathf.Clamp(64 * (camImageSize/360), 64, 128);
			float indicatorBorder = imageRect.width * 0.056f;
			Vector3 vesForward = vessel.ReferenceTransform.up;
			Vector3 upDirection = (transform.position-FlightGlobals.currentMainBody.transform.position).normalized;
			//horizon indicator
			float horizY = imageRect.y+imageRect.height-indicatorSize-indicatorBorder;
			Vector3 hForward = Vector3.ProjectOnPlane(vesForward, upDirection);
			float hAngle = -Misc.SignedAngle(hForward, vesForward, upDirection);
			horizY -= (hAngle/90) * (indicatorSize/2);
			Rect horizonRect = new Rect(indicatorBorder + imageRect.x, horizY, indicatorSize, indicatorSize);
			GUI.DrawTexture(horizonRect, BDArmorySettings.Instance.horizonIndicatorTexture, ScaleMode.StretchToFill, true);

			//roll indicator
			Rect rollRect = new Rect(indicatorBorder+imageRect.x, imageRect.y+imageRect.height-indicatorSize-indicatorBorder, indicatorSize, indicatorSize);
			GUI.DrawTexture(rollRect, rollReferenceTexture, ScaleMode.StretchToFill, true);
			Vector3 localUp = vessel.ReferenceTransform.InverseTransformDirection(upDirection);
			localUp = Vector3.ProjectOnPlane(localUp, Vector3.up).normalized;
			float rollAngle = -Misc.SignedAngle(-Vector3.forward, localUp, Vector3.right);
			GUIUtility.RotateAroundPivot(rollAngle, rollRect.center);
			GUI.DrawTexture(rollRect, rollIndicatorTexture, ScaleMode.StretchToFill, true);
			GUI.matrix = Matrix4x4.identity;

			//target direction indicator
			float angleToTarget = Misc.SignedAngle(hForward, Vector3.ProjectOnPlane(targetPointPosition-transform.position, upDirection), Vector3.Cross(upDirection, hForward));
			GUIUtility.RotateAroundPivot(angleToTarget, rollRect.center);
			GUI.DrawTexture(rollRect, BDArmorySettings.Instance.targetDirectionTexture, ScaleMode.StretchToFill, true);
			GUI.matrix = Matrix4x4.identity;




			//resizing
			Rect resizeRect = new Rect(camWindowRect.width-20, camWindowRect.height-20, 20, 20);
			if(GUI.RepeatButton(resizeRect, "//"))
			{
				resizing = true;
			}

			if(resizing)
			{
				camImageSize += Mouse.delta.x/4;
				camImageSize += Mouse.delta.y/4;

				camImageSize = Mathf.Clamp(camImageSize, 360,800);
				

				RefreshWindowSize();
			}

			if(Input.GetMouseButtonUp(0))
			{
				resizing = false;
			}
		}



		void SlaveTurrets()
		{
			foreach (var mtc in vessel.FindPartModulesImplementing<ModuleTargetingCamera>())
			{
				mtc.slaveTurrets = false;
			}

			foreach (var rad in vessel.FindPartModulesImplementing<ModuleRadar>())
			{
				rad.slaveTurrets = false;
			}

			slaveTurrets = true;
		}

		void UnslaveTurrets()
		{
			foreach (var mtc in vessel.FindPartModulesImplementing<ModuleTargetingCamera>())
			{
				mtc.slaveTurrets = false;
			}

			foreach (var rad in vessel.FindPartModulesImplementing<ModuleRadar>())
			{
				rad.slaveTurrets = false;
			}
		}


		void RefreshWindowSize()
		{
			float windowWidth = camImageSize+16;
			float windowHeight = camImageSize+8+controlPanelHeight;
			camWindowRect = new Rect(camWindowRect.x, camWindowRect.y, windowWidth, windowHeight);
		}

		void SlewCamera(Vector3 direction)
		{
			StartCoroutine(SlewCamRoutine(direction));
		}

		IEnumerator SlewCamRoutine(Vector3 direction)
		{
			StopResetting();
			StopPointToPosRoutine();
			radarLock = false;
			float slewRate = 35 * fov/60;
			cameraParentTransform.localRotation *= Quaternion.AngleAxis(slewRate*Time.deltaTime, Quaternion.AngleAxis(90, Vector3.forward) * direction);

			yield return new WaitForEndOfFrame();

			if(groundStabilized)
			{
				GroundStabilize();
			}
		}

		void PointToGPSTarget()
		{
			if(weaponManager && weaponManager.designatedGPSCoords != Vector3d.zero)
			{
				StartCoroutine(PointToPositionRoutine(VectorUtils.GetWorldSurfacePostion(weaponManager.designatedGPSCoords, vessel.mainBody)));
			}
		}

		void ZoomIn()
		{
			StopResetting();
			if(currentFovIndex < zoomFovs.Length-1)
			{
				currentFovIndex++;
			}

			//fov = zoomFovs[currentFovIndex];
		}

		void ZoomOut()
		{
			StopResetting();
			if(currentFovIndex > 0)
			{
				currentFovIndex--;
			}
			
			//fov = zoomFovs[currentFovIndex];
		}

		void GroundStabilize()
		{
			if(vessel.packed) return;
			StopResetting();

			RaycastHit rayHit;
			Ray ray = new Ray(cameraParentTransform.position + (50*cameraParentTransform.forward), cameraParentTransform.forward);
			if(Physics.Raycast(ray, out rayHit, maxRayDistance-50, 557057))
			{
				groundStabilized = true;
				groundTargetPosition = rayHit.point;

				if(CoMLock)
				{
					Part p = rayHit.collider.GetComponentInParent<Part>();
					if(p && p.vessel && p.vessel.CoM!=Vector3.zero)
					{
						groundTargetPosition = p.vessel.CoM;
						StartCoroutine(StabilizeNextFrame());
					}
				}
				Vector3d newGTP = VectorUtils.WorldPositionToGeoCoords(groundTargetPosition, vessel.mainBody);
				if(newGTP!=Vector3d.zero)
				{
					bodyRelativeGTP = newGTP;
				}
            }
			else
			{
				Vector3 upDir = VectorUtils.GetUpDirection(transform.position);
				float altitude = (float)vessel.altitude; //MissileGuidance.GetRadarAltitude(vessel);
				Plane surfacePlane = new Plane(upDir, vessel.transform.position-(altitude*upDir));
				float enter;
				if(surfacePlane.Raycast(ray, out enter))
				{
					if(enter > 0)
					{
						groundStabilized = true;
						groundTargetPosition = ray.GetPoint(enter);
						Vector3d newGTP = VectorUtils.WorldPositionToGeoCoords(groundTargetPosition, vessel.mainBody);
						if(newGTP != Vector3d.zero)
						{
							bodyRelativeGTP = newGTP;
						}
					}
				}
	
			}
		}

		IEnumerator StabilizeNextFrame()
		{
			yield return new WaitForFixedUpdate();
			yield return new WaitForEndOfFrame();
			if(!gimbalLimitReached && surfaceDetected)
			{
				GroundStabilize();
			}
		}


		void GetHitPoint()
		{
			if(vessel.packed) return;
			if(delayedEnabling) return;

			RaycastHit rayHit;
			Ray ray = new Ray(cameraParentTransform.position + (50*cameraParentTransform.forward), cameraParentTransform.forward);
			if(Physics.Raycast(ray, out rayHit, maxRayDistance-50, 557057))
			{
				targetPointPosition = rayHit.point;

				if(!surfaceDetected && groundStabilized && !gimbalLimitReached)
				{
					groundStabilized = true;
					groundTargetPosition = rayHit.point;
					
					if(CoMLock)
					{
						Part p = rayHit.collider.GetComponentInParent<Part>();
						if(p && p.vessel && p.vessel.Landed)
						{
							groundTargetPosition = p.vessel.CoM;
						}
					}
					Vector3d newGTP = VectorUtils.WorldPositionToGeoCoords(groundTargetPosition, vessel.mainBody);
					if(newGTP != Vector3d.zero)
					{
						bodyRelativeGTP = newGTP;
					}
				}

				surfaceDetected = true;

				if(groundStabilized && !gimbalLimitReached && CMDropper.smokePool!=null)
				{
					if(CMSmoke.RaycastSmoke(ray))
					{
						surfaceDetected = false;
					}
				}

			}
			else
			{
				targetPointPosition = cameraParentTransform.position + (maxRayDistance*cameraParentTransform.forward);
				surfaceDetected = false;
			}
		}

		void ClearTarget()
		{
			groundStabilized = false;
		}


		IEnumerator ResetCamera()
		{
			resetting = true;
			radarLock = false;
			StopPointToPosRoutine();

			if(groundStabilized)
			{
				ClearTarget();
			}

			currentFovIndex = 0;
			//fov = zoomFovs[currentFovIndex];

			while(Vector3.Angle(cameraParentTransform.forward, cameraParentTransform.parent.forward) > 0.1f)
			{
				Vector3 newForward = Vector3.RotateTowards(cameraParentTransform.forward, cameraParentTransform.parent.forward, 60*Mathf.Deg2Rad*Time.deltaTime, 0);
				cameraParentTransform.rotation = Quaternion.LookRotation(newForward, VectorUtils.GetUpDirection(transform.position));
				gimbalLimitReached = false;
				yield return null;
			}
			resetting = false;
		}

		void StopPointToPosRoutine()
		{
			if(slewingToPosition)
			{
				StartCoroutine(StopPTPRRoutine());
			}
		}

		IEnumerator StopPTPRRoutine()
		{
			stopPTPR = true;
			yield return null;
			yield return new WaitForEndOfFrame();
			stopPTPR = false;
		}

		bool stopPTPR = false;
		bool slewingToPosition = false;
		public IEnumerator PointToPositionRoutine(Vector3 position)
		{
			stopPTPR = false;
			slewingToPosition = true;
			radarLock = false;
			StopResetting();
			ClearTarget();
			while(!stopPTPR && Vector3.Angle(cameraParentTransform.transform.forward, position - (cameraParentTransform.transform.position+(part.rb.velocity*Time.fixedDeltaTime))) > 0.75f)
			{
				Vector3 newForward = Vector3.RotateTowards(cameraParentTransform.transform.forward, position - cameraParentTransform.transform.position, 60 * Mathf.Deg2Rad * Time.fixedDeltaTime, 0);
				cameraParentTransform.rotation = Quaternion.LookRotation(newForward, VectorUtils.GetUpDirection(transform.position));

				yield return new WaitForFixedUpdate();
				if(gimbalLimitReached)
				{
					ClearTarget();
					StartCoroutine("ResetCamera");
					slewingToPosition = false;
					yield break;
				}
			}
			if(surfaceDetected && !stopPTPR)
			{
				cameraParentTransform.transform.rotation = Quaternion.LookRotation(position - cameraParentTransform.position, VectorUtils.GetUpDirection(transform.position));
				GroundStabilize();
			}
			slewingToPosition = false;
			yield break;
		}

		void StopResetting()
		{
			if(resetting)
			{
				StopCoroutine("ResetCamera");
				resetting = false;
			}
		}

		void ParseFovs()
		{
			/*
			string[] fovStrings = zoomFOVs.Split(new char[]{','});
			zoomFovs = new float[fovStrings.Length];
			for(int i = 0; i < fovStrings.Length; i++)
			{
				zoomFovs[i] = float.Parse(fovStrings[i]);
			}*/

			zoomFovs = Misc.ParseToFloatArray(zoomFOVs);
		}

		void OnDestroy()
		{
			windowIsOpen = false;
		}

		Vector2 TargetAzimuthElevationScreenPos(Rect screenRect, Vector3 targetPosition, float textureSize)
		{
			Vector3 localPos = vessel.ReferenceTransform.InverseTransformPoint(targetPosition);
			Vector3 aziRef = Vector3.up;
			Vector3 aziPos = Vector3.ProjectOnPlane(localPos, Vector3.forward);
			float elevation = VectorUtils.SignedAngle(aziPos, localPos, Vector3.forward);
			float normElevation = elevation / 70;


			float azimuth = VectorUtils.SignedAngle(aziRef, aziPos, Vector3.right);
			float normAzimuth = Mathf.Clamp(azimuth / 120, -1, 1);

			float x = screenRect.x + (screenRect.width/2) + (normAzimuth * (screenRect.width / 2)) - (textureSize/2);
			float y = screenRect.y + (screenRect.height/4) + (normElevation * (screenRect.height / 4)) - (textureSize/2);

			x = Mathf.Clamp(x, textureSize / 2, screenRect.width - (textureSize / 2));
			y = Mathf.Clamp(y, textureSize / 2, (screenRect.height) - (textureSize / 2));

			return new Vector2(x, y);
		}




	}
}
