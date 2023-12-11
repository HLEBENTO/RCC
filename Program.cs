using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.GameSystems;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.Policy;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRage.Scripting;
using VRageMath;

namespace IngameScript
{
partial class Program : MyGridProgram
{
//=============================================================
//There's a little bit left over from PAS, since I was basing it on it
#region Global
string WorkMode = "car", LastWorkMode = "";
bool Init = true, Init2 = true, changeMode = false;

string[] LCD_TAG = { "RCC_", "Tires_", "Nav_", "Car_", "Base_", "Log_" };
string RC_TAG = "Autopilot", Include_TAG = "Include";
string WB_TAG = "Waypoints";
string SB_TAG = "RCC_Indicator";
const string SL_TAG = "RCC_Signal", Sensor_TAG = "RCC_Boost";

const double timeLimit = 0.1, blocksUpd = 10, errUpd = 15, iniUpd = 1, statisticsUpd = 0.5;
double timeForUpdateAP = 0, timeForUpdateINI = 0, timeForUpdateBlocks = 0, timeToClearErrors = 0, timeForAlarmDelay = 0, timeForUpdateStatistics = 0;
bool wasMainPartExecuted = false;
string shutdownLog = "";

RuntimeProfiler RP;
SoundBlock SB;
Controller controller;
Transponder transponder;
DisplayScheduler displayScheduler;
RaceBase raceBase;

MyIni Ini;
MyIni StorageIni;
public Program() { Initialize(); }
public void Initialize()
{
	WorkMode = "car"; LastWorkMode = ""; Init = true; Init2 = true; wasMainPartExecuted = false; timeForUpdateAP = timeForUpdateINI = timeForUpdateBlocks = 0;
	RP = null; SB = null; controller = null; transponder = null; displayScheduler = null; raceBase = null;

	bool empty = Me.CustomData == "";
	Ini = new MyIni(); StorageIni = new MyIni();
	ParseStorage();
	
	RP = new RuntimeProfiler(this);
	SB = new SoundBlock(this, SB_TAG);
	transponder = new Transponder(IGC, WorkMode, SB);
	if (WorkMode == "car")
	{
		controller = new Controller(this, RC_TAG, WB_TAG, Include_TAG, Me, Ini, Runtime, transponder, SB);
		displayScheduler = new DisplayScheduler(this, RP, LCD_TAG, WorkMode, controller, transponder, SB, SB_TAG);
		controller.CurrentTire.UpdateFriction();
	}
	if (WorkMode == "base")
	{
		raceBase = new RaceBase(this, transponder, SB);
		displayScheduler = new DisplayScheduler(this, RP, LCD_TAG, WorkMode, raceBase, transponder, SB, SB_TAG);
	}
	Save();
	if (WorkMode == "car" && empty) LoadTemplate();
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
}
#endregion
#region ini
void ParseStorage()
{
	if (Storage == "") return;
	if (!StorageIni.TryParse(Storage))
	{
		Storage = "";
		throw new Exception("Load failed. Storage has been cleared. Recompile the script.");
	}


	WorkMode = StorageIni.Get("Work Mode", "Car or Base").ToString(WorkMode);


	if (Init) { StorageIni.Set("Work Mode", "Car or Base", WorkMode); LastWorkMode = WorkMode; Storage = StorageIni.ToString(); Init = false; return; }
	if (WorkMode != LastWorkMode) Initialize();


	if (WorkMode == "car")
	{
		if (Init2) shutdownLog = StorageIni.Get("Storage", "Log").ToString();
		else shutdownLog = controller.CreateSaveLog(false);

		controller.pN = StorageIni.Get("Storage", "Pilot Number").ToInt32(controller.pN);
		Vector3D.TryParse(StorageIni.Get("Storage", "My Base").ToString(), out controller.MyBase);
		Vector3D.TryParse(StorageIni.Get("Storage", "Pitlane Start").ToString(), out controller.pitLaneStart);
		Vector3D.TryParse(StorageIni.Get("Storage", "Pitlane End").ToString(), out controller.pitLaneEnd);


		controller.pitLaneTreshold = StorageIni.Get("Storage", "Pitlane Treshold").ToDouble(controller.pitLaneTreshold);
		controller.baseTreshold = StorageIni.Get("Storage", "Base Treshold").ToDouble(controller.baseTreshold);
		controller.speedLimit = StorageIni.Get("Storage", "Pitlane Speed Limit").ToDouble(controller.speedLimit);
		controller.userPower = StorageIni.Get("Storage", "User Power").ToDouble(controller.userPower);
		controller.userSpeed = StorageIni.Get("Storage", "User Speed").ToDouble(controller.userSpeed);
		controller.password = StorageIni.Get("Storage", "Password").ToString(controller.password);

		controller.sensorBoostAllowed = StorageIni.Get("Storage", "Sensor Boost Allowed").ToBoolean(controller.sensorBoostAllowed);
		controller.sensorBoostSpeedTreshold = StorageIni.Get("Storage", "Sensor Boost Speed Treshold").ToDouble(controller.sensorBoostSpeedTreshold);
		controller.boostEngageTime = StorageIni.Get("Storage", "Boost Engage Time").ToDouble(controller.boostEngageTime);

		controller.SetTireFromStorage(StorageIni.Get("Storage", "Current Tire Type").ToString(), StorageIni.Get("Storage", "Current Tire Wear").ToDouble());
		ParseTiresList(StorageIni.Get("Storage", "Tires List").ToString());
		controller.UpdateTiresList();
		controller.CreatePitLaneProperties();
	}
	if(WorkMode == "base")
	{
		ParseCarsList(StorageIni.Get("Storage", "Cars List").ToString());
	}
}

void SaveStorage()
{
	StorageIni.Set("Work Mode", "Car or Base", WorkMode);
	if (changeMode) { Storage = StorageIni.ToString(); changeMode = false; return; }
	if (WorkMode == "car")
	{
		StorageIni.Set("Storage", "Log", Init2 ? "" : shutdownLog);
		StorageIni.Set("Storage", "Pilot Number", controller.pN);
		StorageIni.Set("Storage", "My Base", controller.MyBase.ToString());
		StorageIni.Set("Storage", "Pitlane Start", controller.pitLaneStart.ToString());
		StorageIni.Set("Storage", "Pitlane End", controller.pitLaneEnd.ToString());
		StorageIni.Set("Storage", "Pitlane Treshold", controller.pitLaneTreshold);
		StorageIni.Set("Storage", "Base Treshold", controller.baseTreshold);
		StorageIni.Set("Storage", "Pitlane Speed Limit", controller.speedLimit);
		StorageIni.Set("Storage", "User Power", controller.userPower);
		StorageIni.Set("Storage", "User Speed", controller.userSpeed);
		StorageIni.Set("Storage", "Password", controller.password);

		StorageIni.Set("Storage", "Sensor Boost Allowed", controller.sensorBoostAllowed);
		StorageIni.Set("Storage", "Sensor Boost Speed Treshold", controller.sensorBoostSpeedTreshold);
		StorageIni.Set("Storage", "Boost Engage Time", controller.boostEngageTime);

		StorageIni.Set("Storage", "Current Tire Type", controller.CurrentTire.Type.ToString());
		StorageIni.Set("Storage", "Current Tire Wear", controller.CurrentTire.Wear);

		string tireSets = "";
		foreach (var tire in controller.tireSets)
		{
			tireSets += tire.Type.ToString() + ':' + tire.Wear.ToString();
			if (!tire.Equals(controller.tireSets.Last())) tireSets += ";";
		}
		StorageIni.Set("Storage", "Tires List", tireSets);
		controller.UpdateTiresList();
	}
	if(WorkMode == "base")
	{
		string carList = "";
		foreach (var car in raceBase.Cars)
		{
			if (car.PN == 0) continue;
			carList += car.PN.ToString()
				+ ':' + car.TimeOffline.ToString()
				+ ':' + car.CurrentTire
				+ ':' + car.CurrentWear.ToString()
				+ ':' + car.FuelPercent.ToString()
				+ ':' + car.IsOnPitlane.ToString()
				+ ':' + car.IsSpeedLimited.ToString()
				+ ':' + car.IsBoostOn.ToString()
				+ ':' + car.IsDisqualified.ToString();
			if (!car.Equals(raceBase.Cars.Last())) carList += ";";
		}
		StorageIni.Set("Storage", "Cars List", carList);
	}
	Storage = StorageIni.ToString();
}

void ParseCustomDataIni()
{
	if (!Ini.TryParse(Me.CustomData))
	{
		Me.CustomData = "";
		throw new Exception("Load failed. Custom Data has been cleared. Recompile the script.");
	}

	if (WorkMode == "car" && !Init)
	{
		controller.pN = Ini.Get("Storage", "Pilot Number").ToInt32(controller.pN);
		controller.MyBase = ParsePoint(Ini.Get("Storage", "My Base").ToString());
		controller.pitLaneStart = ParsePoint(Ini.Get("Storage", "Pitlane Start").ToString());
		controller.pitLaneEnd = ParsePoint(Ini.Get("Storage", "Pitlane End").ToString());
		controller.pitLaneTreshold = Ini.Get("Storage", "Pitlane Treshold").ToDouble(controller.pitLaneTreshold);
		controller.baseTreshold = Ini.Get("Storage", "Base Treshold").ToDouble(controller.baseTreshold);
		controller.speedLimit = Ini.Get("Storage", "Pitlane Speed Limit").ToDouble(controller.speedLimit);

		controller.sensorBoostAllowed = Ini.Get("Storage", "Sensor Boost Allowed").ToBoolean(controller.sensorBoostAllowed);
		controller.sensorBoostSpeedTreshold = Ini.Get("Storage", "Sensor Boost Speed Treshold").ToDouble(controller.sensorBoostSpeedTreshold);
		controller.boostEngageTime = Ini.Get("Storage", "Boost Engage Time").ToDouble(controller.boostEngageTime);



		controller.SetTireFromStorage(Ini.Get("Storage", "Current Tire Type").ToString(), Ini.Get("Storage", "Current Tire Wear").ToDouble());
		ParseTiresList(Ini.Get("Storage", "Tires List").ToString());

		controller.CreatePitLaneProperties();
		SaveStorage();
	}
}

bool ParseTiresList(string list)
{
	if (list == "") return false;
	controller.tireSets.Clear();
	string[] lines = list.Split(';');
	bool Success = true;
	foreach (var line in lines)
	{
		string[] elements = line.Split(':');
		if (elements.Length != 2) continue;
		Controller.TireType type = Controller.TireType.Hard;
		bool success = Enum.TryParse(elements[0], out type);
		if (success)
		{
			Controller.Tire newTire = Controller.Tire.SetTire(type);

			double wear = 0;
			Double.TryParse(elements[1], out wear);
			newTire.SetWear(wear);
			controller.tireSets.Add(newTire);
		}
		else Success = false;
	}
	return Success;
}

void ParseCarsList(string list)
{
	if (list == "") return;
	raceBase.Cars.Clear();
	string[] lines = list.Split(';');
	foreach (var line in lines)
	{
		string[] elements = line.Split(':');
		int testZero = 0;
		if (!Int32.TryParse(elements[0], out testZero) || testZero == 0) continue;
		Car newCar = new Car() {
			PN = Int32.Parse(elements[0]),
			TimeOffline = double.Parse(elements[1]),
			CurrentTire = elements[2],
			CurrentWear = double.Parse(elements[3]),
			FuelPercent = double.Parse(elements[4]),
			IsOnPitlane = bool.Parse(elements[5]),
			IsSpeedLimited = bool.Parse(elements[6]),
			IsBoostOn = bool.Parse(elements[7]),
			IsDisqualified = bool.Parse(elements[8]),
			Log = "",
			OldWear = 0,
			OldTire = elements[2]
		};
		raceBase.Cars.Add(newCar);
	}
}

Vector3D ParsePoint(string line)
{
	Vector3D waypoint = Vector3D.Zero;
	string[] elements = line.Split(':');
	double X, Y, Z;
	if (double.TryParse(elements[2], out X) && double.TryParse(elements[3], out Y) && double.TryParse(elements[4], out Z))
	waypoint = new Vector3D(X, Y, Z);
	return waypoint;
}

public void ParseIni()
{
	//ini parsing.
	if (!Ini.TryParse(Me.CustomData))
	{
		Me.CustomData = "";
		throw new Exception("Initialisation failed. CustomData has been cleared. Recompile the script.");
	}
	//SB_TAG = Ini.Get("Settings", "Sound Block Name").ToString(SB_TAG);
	if (WorkMode == "car")
	{
		controller.useSensorBoost = Ini.Get("Settings", "Use Sensor Boost").ToBoolean(controller.useSensorBoost);
	}
	if (WorkMode == "base")
	{
		//SB.ServiceEnabled = Ini.Get("Airport Settings", "Use Sound Block").ToBoolean(SB.ServiceEnabled);
	}
}
public void Save()
{
	ParseIni();
	ParseStorage();
	if (Init2 && WorkMode == "car") transponder.SendLogToBase(controller.pN, shutdownLog);
	SaveStorage();
	
	//Ini.Set("Settings", "Sound Block Name", SB_TAG);
	if (WorkMode == "car")
	{
		Ini.Set("Settings", "Use Sensor Boost", controller.useSensorBoost);
	}
	if (WorkMode == "base")
	{
		//Ini.Set("Airport Settings", "Use Sound Block", SB.ServiceEnabled);
	}

	Me.CustomData = Ini.ToString();
}

void LoadTemplate()
{
	Ini.Set("Storage", "Pilot Number", controller.pN);
	Ini.Set("Storage", "My Base", CreateGPSFormatPoint(controller.MyBase));
	Ini.Set("Storage", "Pitlane Start", CreateGPSFormatPoint(controller.pitLaneStart));
	Ini.Set("Storage", "Pitlane End", CreateGPSFormatPoint(controller.pitLaneEnd));
	Ini.Set("Storage", "Pitlane Treshold", controller.pitLaneTreshold);
	Ini.Set("Storage", "Base Treshold", controller.baseTreshold);
	Ini.Set("Storage", "Pitlane Speed Limit", controller.speedLimit);
	Ini.Set("Storage", "Sensor Boost Allowed", controller.sensorBoostAllowed);
	Ini.Set("Storage", "Sensor Boost Speed Treshold", controller.sensorBoostSpeedTreshold);
	Ini.Set("Storage", "Boost Engage Time", controller.boostEngageTime);

	Ini.Set("Storage", "Current Tire Type", controller.CurrentTire.Type.ToString());
	Ini.Set("Storage", "Current Tire Wear", controller.CurrentTire.Wear);
	Ini.Set("Storage", "Tires List", CreateTiresList());
	Me.CustomData = Ini.ToString();
}

string CreateGPSFormatPoint(Vector3D point)
{
	return "GPS:Example:"+Math.Round(point.X, 2)+':'+ Math.Round(point.Y, 2) + ':'+ Math.Round(point.Z, 2) + ":#FF75C9F1:";
}
string CreateTiresList()
{
	if (controller.tireSets.Count == 0) return "Hard:0;Medium:0;Medium:0;Soft:0;Soft:0;Supersoft:0;Ultrasoft:0";
	string tireSets = "";
	foreach (var tire in controller.tireSets)
	{
		tireSets += tire.Type.ToString() + ':' + tire.Wear.ToString();
		if (!tire.Equals(controller.tireSets.Last())) tireSets += ";";
	}
	return tireSets;
}
#endregion
#region Main
public void Main(string argument, UpdateType uType)
{
	double lastRun = Runtime.TimeSinceLastRun.TotalSeconds;
	timeForUpdateAP += lastRun; timeForUpdateINI += lastRun; timeForUpdateBlocks += lastRun; timeForAlarmDelay += lastRun; timeForUpdateStatistics += lastRun;
	if (WorkMode == "car") { if (controller.ErrListNotEmpty()) timeToClearErrors += lastRun; if (controller.boostEngaged) controller.timeSinceBoostEngaged += lastRun; }
	if (wasMainPartExecuted) { RP.saveRuntime(); wasMainPartExecuted = false; }
	#region Arguments
	if (uType == UpdateType.Terminal || uType == UpdateType.Script || uType == UpdateType.Trigger || uType == UpdateType.IGC)
	{
		if (WorkMode == "car")
		{
			string[] parsedArg = argument.Split(' ');
			if (parsedArg.Length == 2)
			switch (parsedArg[0].ToLower())
			{
				case "load": if (controller.password == parsedArg[1]) { ParseCustomDataIni(); SB.Request("Yes"); } else SB.Request("No"); break;
				case "clear": if (controller.password == parsedArg[1]) { Storage = ""; SB.Request("Yes"); } else SB.Request("No"); break;
				case "workmode": if (controller.password == parsedArg[1]) { WorkMode = "base"; changeMode = true; SaveStorage(); Initialize(); } break;
				default: break;
			}
			else switch (argument.ToLower())
			{
				case "ultra": controller.RequestTire(Controller.TireType.Ultrasoft); break;
				case "super": controller.RequestTire(Controller.TireType.Supersoft); break;
				case "soft": controller.RequestTire(Controller.TireType.Soft); break;
				case "medium": controller.RequestTire(Controller.TireType.Medium); break;
				case "hard": controller.RequestTire(Controller.TireType.Hard); break;
						
				case "abort": controller.AbortTireRequest(); break;

				case "objcomp": SB.Request("ObjComp"); break;

				case "load": if (controller.password == "0") { ParseCustomDataIni(); SB.Request("Yes"); } else SB.Request("No"); break;
				case "unload": LoadTemplate(); break;

				case "clear": if (controller.password == "0") { Storage = ""; SB.Request("Yes"); } else SB.Request("No"); break;
				case "workmode": if (controller.password == "0") { WorkMode = "base"; changeMode = true; SaveStorage(); Initialize();} break;
				case "debug": IMyTextPanel debugPanel = GridTerminalSystem.GetBlockWithName("!debug") as IMyTextPanel; debugPanel?.WriteText(Storage); break;
				case "debug2": IMyTextPanel debugPanel2 = GridTerminalSystem.GetBlockWithName("!debug") as IMyTextPanel;
							//List<ITerminalProperty> props = new List<ITerminalProperty>();
							//controller.engines.First().GetProperties(props);
								
							string text = "";
							//foreach (var prop in props) text += prop.Id.ToString() + '\n';
							//text = controller.GetHydrogenAmount(controller.engines.First().DetailedInfo).ToString();
							//foreach (var gen in controller.gasBlocks) text += gen.BlockDefinition.ToString() + '\n';
							foreach(var wheel in controller.wheels) {  text += "Name: " + wheel.CustomName.ToString() + "\n";
									text += "Forward: " + (wheel.WorldMatrix.Forward == controller.RC.WorldMatrix.Forward) + "\n";
									text += "Backward: " + (wheel.WorldMatrix.Backward == controller.RC.WorldMatrix.Forward) + "\n";
									text += "Left: " + (wheel.WorldMatrix.Left == controller.RC.WorldMatrix.Forward) + "\n";
									text += "Right: " + (wheel.WorldMatrix.Right == controller.RC.WorldMatrix.Forward) + "\n";
									text += "Up: " + (wheel.WorldMatrix.Up == controller.RC.WorldMatrix.Forward) + "\n";
									text += "Down: " + (wheel.WorldMatrix.Down == controller.RC.WorldMatrix.Forward) + "\n";
									text += "Forward To Up: " + (wheel.WorldMatrix.Forward == controller.RC.WorldMatrix.Up) + "\n";
									text += "Backward To Up: " + (wheel.WorldMatrix.Backward == controller.RC.WorldMatrix.Up) + "\n";
									text += "Left To Up: " + (wheel.WorldMatrix.Left == controller.RC.WorldMatrix.Up) + "\n";
									text += "Right To Up: " + (wheel.WorldMatrix.Right == controller.RC.WorldMatrix.Up) + "\n";
									text += "Up To Up: " + (wheel.WorldMatrix.Up == controller.RC.WorldMatrix.Up) + "\n";
									text += "Down To Up: " + (wheel.WorldMatrix.Down == controller.RC.WorldMatrix.Up) + "\n";

								}
							debugPanel2?.WriteText(text); break;
				default: break;
			}
			
		}
		else if (WorkMode == "base")
		{
			string[] parsedArg = argument.Split(' ');
			if (parsedArg.Length == 4)
			switch (parsedArg[0].ToLower())
			{
				case "send": transponder.SendToCars(parsedArg[1], parsedArg[2], parsedArg[3]); break;
				default: break;
			}
			if (parsedArg.Length == 2)
			switch (parsedArg[0].ToLower())
			{
				case "undsq": int c = 100; if(Int32.TryParse(parsedArg[1], out c)) raceBase.UnDSQ(c); break;
				default: break;
			}
			else switch (argument.ToLower())
			{
				case "workmode": WorkMode = "car"; changeMode = true; SaveStorage(); Initialize(); break;
				case "clear": StorageIni.Set("Storage", "Cars List", ""); Storage = StorageIni.ToString(); SB.Request("Yes"); break;
				default: break;
			}

		}
		switch (argument.ToLower()) {
			case "toggle": Runtime.UpdateFrequency = (Runtime.UpdateFrequency == UpdateFrequency.Update1) ? UpdateFrequency.None : UpdateFrequency.Update1; break;
			case "recompile": Initialize(); break;
			default: break; }
	}
	#endregion
	if (timeForAlarmDelay >= SB.CurDelay) { timeForAlarmDelay = 0; SB.update(); }
	if (timeForUpdateINI >= iniUpd)
	{
		timeForUpdateINI = 0;
		if (WorkMode == "car" && Init2) { transponder.SendLogToBase(controller.pN, controller.CreateSaveLog(true)); Init2 = false; }
		ParseIni();
		SaveStorage();
	}
	if (timeForUpdateBlocks >= blocksUpd)
	{
		timeForUpdateBlocks = 0;
		if (WorkMode == "car") { controller.UpdateBlocks();}
		if (WorkMode == "base") raceBase.UpdateBlocks();
		SB.updateBlocks(SB_TAG);
		displayScheduler.updateBlocks();
		wasMainPartExecuted = true;
	}
	if (timeToClearErrors >= errUpd)
	{
		timeToClearErrors = 0;
		if (WorkMode == "car") controller.ClearErrorList();
	}
	if (timeForUpdateStatistics >= statisticsUpd)
	{
		timeForUpdateStatistics = 0;
		if (WorkMode == "car") { controller.Update1(); }
		if (WorkMode == "base") raceBase.Update1();
		wasMainPartExecuted = true;
	}
	if (timeForUpdateAP >= timeLimit)
	{
		timeForUpdateAP = 0;
		if (WorkMode == "car") { controller.Update();}
		if (WorkMode == "base") raceBase.Update();
		RP.update();
		displayScheduler.ShowStatus();
		wasMainPartExecuted = true;
	}
}
#endregion
#region Runtime
class RuntimeProfiler
{
	Program Parent; double EMA_A = 0.003; public double lastMainPartRuntime = 0, RunTimeAvrEMA = 0;
	public RuntimeProfiler(Program parent) { Parent = parent; }
	public void update() { RunTimeAvrEMA = Math.Round(EMA_A * lastMainPartRuntime + (1 - EMA_A) * RunTimeAvrEMA, 4); }
	public void saveRuntime() { lastMainPartRuntime = Parent.Runtime.LastRunTimeMs; }
}
#endregion
#region DisplayScheduler
class DisplayScheduler
{
	StringBuilder MyScreenData;
	Program Parent;
	RuntimeProfiler RP;
	string[] LCD_TAG;
	string WorkMode, SBtag;
	Controller controller;
	RaceBase raceBase;
	Transponder tp;
	SoundBlock SB;
	IMyTextSurface MyScreen;
	List<IMyTextSurface> TiresLCDs, NavLCDs, CarLCDs, BaseLCDs, LogLCDs;
	public DisplayScheduler(Program parent, RuntimeProfiler rt, string[] lcd_tag, string workmode, Controller AP, Transponder Trans, SoundBlock sb, string sbtag)
	{
		Parent = parent; RP = rt; LCD_TAG = lcd_tag; WorkMode = workmode; controller = AP; tp = Trans; SB = sb; SBtag = sbtag;
		TiresLCDs = new List<IMyTextSurface>(); NavLCDs = new List<IMyTextSurface>(); CarLCDs = new List<IMyTextSurface>();
		updateBlocks(); MyScreenData = new StringBuilder();
	}
	public DisplayScheduler(Program parent, RuntimeProfiler rt, string[] lcd_tag, string workmode, RaceBase rb, Transponder Trans, SoundBlock sb, string sbtag)
	{
		Parent = parent; RP = rt; LCD_TAG = lcd_tag; WorkMode = workmode; raceBase = rb; tp = Trans; SB = sb; SBtag = sbtag;
		BaseLCDs = new List<IMyTextSurface>(); LogLCDs = new List<IMyTextSurface>();
		updateBlocks(); MyScreenData = new StringBuilder();
	}

	public void updateBlocks()
	{
		if (WorkMode == "car")
		{
			TiresLCDs.Clear(); NavLCDs.Clear(); CarLCDs.Clear();

			List<IMyTerminalBlock> lcdHosts = new List<IMyTerminalBlock>();
			Parent.GridTerminalSystem.GetBlocksOfType(lcdHosts, block => block as IMyTextSurfaceProvider != null && block.IsSameConstructAs(Parent.Me));
			List<string> lines = new List<string>();
			IMyTextSurface lcd;

			foreach (var block in lcdHosts)
			{
				if (block.CustomData.Contains(LCD_TAG[0]))
				{
					lines.Clear();
					new StringSegment(block.CustomData).GetLines(lines);
					foreach (var line in lines)
					{
						if (line.Contains(LCD_TAG[0]))
						{
							for (int i = 1; i < LCD_TAG.Length; i++)
							{
								if (line.Contains(LCD_TAG[i]))
								{
									if (block as IMyTextSurface != null)
										lcd = block as IMyTextSurface;
									else
									{
										int displayIndex = 0;
										int.TryParse(line.Replace(LCD_TAG[0] + LCD_TAG[i], ""), out displayIndex);
										IMyTextSurfaceProvider t_sp = block as IMyTextSurfaceProvider;
										displayIndex = Math.Max(0, Math.Min(displayIndex, t_sp.SurfaceCount));
										lcd = t_sp.GetSurface(displayIndex);
									}
									lcd.ContentType = ContentType.TEXT_AND_IMAGE;
									switch (i)
									{
										case 1:
											if (lcd.FontSize == 1.000) { lcd.FontSize = 1.1f; lcd.TextPadding = 4; }
											lcd.Font = "Monospace";
											TiresLCDs.Add(lcd);
											break;
										case 2:
											if (lcd.FontSize == 1.000) { lcd.FontSize = 1.1f; lcd.TextPadding = 4; }
											lcd.Font = "Monospace";
											NavLCDs.Add(lcd);
											break;
										case 3:
											if (lcd.FontSize == 1.000) { lcd.FontSize = 2.2f; lcd.TextPadding = 4; }
											lcd.Font = "Monospace";
											CarLCDs.Add(lcd);
											break;
										default:
											break;
									}
									break;
								}
							}
						}
					}
				}
			}
		}
		if (WorkMode == "base")
		{
			BaseLCDs.Clear();
			List<IMyTerminalBlock> lcdHosts = new List<IMyTerminalBlock>();
			Parent.GridTerminalSystem.GetBlocksOfType(lcdHosts, block => block as IMyTextSurface != null && block.IsSameConstructAs(Parent.Me));
			List<string> lines = new List<string>();
			IMyTextSurface lcd;
			foreach (var block in lcdHosts)
			{
				if (block.CustomData.Contains(LCD_TAG[0]))
				{
					lines.Clear();
					new StringSegment(block.CustomData).GetLines(lines);
					foreach (var line in lines)
					{
						if (line.Contains(LCD_TAG[0]))
						{
							for (int i = 1; i < LCD_TAG.Length; i++)
							{
								if (line.Contains(LCD_TAG[i]))
								{
									if (block as IMyTextSurface != null)
										lcd = block as IMyTextSurface;
									else
									{
										int displayIndex = 0;
										int.TryParse(line.Replace(LCD_TAG[0] + LCD_TAG[i], ""), out displayIndex);
										IMyTextSurfaceProvider t_sp = block as IMyTextSurfaceProvider;
										displayIndex = Math.Max(0, Math.Min(displayIndex, t_sp.SurfaceCount));
										lcd = t_sp.GetSurface(displayIndex);
									}
									lcd.ContentType = ContentType.TEXT_AND_IMAGE;
									switch (i)
									{
										case 4:
											if (lcd.FontSize == 1.000) { lcd.FontSize = 0.38f; lcd.TextPadding = 0; }
											lcd.Font = "Monospace";
											BaseLCDs.Add(lcd);
											break;
										case 5:
											if (lcd.FontSize == 1.000) { lcd.FontSize = 0.310f; lcd.TextPadding = 0; }
											lcd.Font = "Monospace";
											LogLCDs.Add(lcd);
											break;
										default:
											break;
									}
									break;
								}
							}
						}
					}
				}
			}
		}
		MyScreen = Parent.Me.GetSurface(0); MyScreen.ContentType = ContentType.TEXT_AND_IMAGE;
	}
	public void ShowStatus()
	{
		MyScreenData.Clear(); MyScreenData.Append("       === RACING CAR CONTROLLER ===");
		if (WorkMode == "car")
		{
			if (!controller.RCReady) MyScreenData.Append("\n Critical Error: Ship Controller not found.\n");
			else
			{
				bool AllDisplays = true;
				string errors = LCD_TAG[0] + " + ";

				if (TiresLCDs.Count > 0) foreach (var thisScreen in TiresLCDs) thisScreen.WriteText(controller.GetTiresList());
				else { errors += LCD_TAG[1] + ", "; AllDisplays = false; }

				if (NavLCDs.Count > 0) foreach (var thisScreen in NavLCDs) thisScreen.WriteText(controller.GetNav());
				else { errors += LCD_TAG[2] + ", "; AllDisplays = false; }

				if (CarLCDs.Count > 0) foreach (var thisScreen in CarLCDs) thisScreen.WriteText(controller.GetCarList());
				else { errors += LCD_TAG[3] + ", "; AllDisplays = false; }

				errors = "\n LCD Error:\n" + errors + "displays were not found.\n";

				if (!AllDisplays) MyScreenData.Append(errors);

				WriteSB();

				MyScreenData.Append("\n Sensor Boost: " + (controller.useSensorBoost && !controller.sensorBoostAllowed ? "Not Allowed" : (!controller.useSensorBoost && controller.sensorBoostAllowed ? "Allowed" : (controller.useSensorBoost && controller.sensorBoostAllowed ? "ON" : (!controller.useSensorBoost && !controller.sensorBoostAllowed ? "OFF" : "")))));
				if(controller.sensorBoostAllowed && !controller.SensorReady) MyScreenData.Append("\n Sensor Boost Error:\n '" + Sensor_TAG + "' Sensor block was not found.\n");

				if(!controller.signalReady) MyScreenData.Append("\n Signal Lights Error:\n '" + SL_TAG + "' Lighting Blocks were not found.\n");

				MyScreenData.Append(controller.ErrListNotEmpty() ? controller.GetErrorsList() : "");
			}
		}
		else if (WorkMode == "base")
		{
			if (BaseLCDs.Count > 0) foreach (var thisScreen in BaseLCDs) thisScreen.WriteText(raceBase.GetCarsList());
			else MyScreenData.Append("\n LCD Error:\n '" + LCD_TAG[0] + LCD_TAG[4] + "' displays were not found.\n");
			if (LogLCDs.Count > 0 ) { if (raceBase.GetLogList().Length > 1) { string currentText = LogLCDs.First().GetText(); currentText += raceBase.GetLogList(); raceBase.ClearLogList(); foreach (var thisScreen in LogLCDs) thisScreen.WriteText(currentText); } }
			else MyScreenData.Append("\n LCD Error:\n '" + LCD_TAG[0] + LCD_TAG[5] + "' displays were not found.\n");
			WriteSB();
		}
		MyScreenData.Append("\n\n Next Update: " + Math.Round(blocksUpd - Parent.timeForUpdateBlocks, 1) + " sec");
		MyScreenData.Append("\n Runtime: L " + RP.lastMainPartRuntime + " ms. Av " + RP.RunTimeAvrEMA + " ms\n Instructions used: " + Parent.Runtime.CurrentInstructionCount + "/" + Parent.Runtime.MaxInstructionCount + "\n");
		MyScreen.WriteText(MyScreenData); Parent.Echo(MyScreenData.ToString());
	}
	void WriteSB()
	{
		if (SB.ServiceEnabled)
		{
			MyScreenData.Append("\n Warning System: ON");
			if (!SB.SReady) MyScreenData.Append("\n Warning System Error:\n '" + SBtag + "' Sound Block not found.\n");
			if (!SB.LReady && !SB.TPReady) MyScreenData.Append("\n Warning System Error:\n '" + SBtag + "' Lighting Block or LCD not found.\n");
		}
		else MyScreenData.Append("\n Warning System: OFF");
	}
}

#endregion
#region RaceBase
class RaceBase
{
	public string StartBeaconTag = "RW_Start", StopBeaconTag = "RW_Stop";
	bool Ready = false;
	Transponder tp;
	Program Parent;
	List<IMyTerminalBlock> Blocks;
	public List<Car> Cars;
	StringBuilder SBCars, SBLog;
	SoundBlock SB;
	IMyTerminalBlock BeaconStart, BeaconStop;
	public RaceBase(Program parent, Transponder trans, SoundBlock sb)
	{
		tp = trans; Parent = parent; SB = sb;
		Blocks = new List<IMyTerminalBlock>();
		Cars = new List<Car>();
		Cars.Clear();
		SBCars = new StringBuilder();
		SBLog = new StringBuilder();
		UpdateBlocks();
	}
	public void Update()
	{
		
	}
	public bool UpdateBlocks()
	{
		if (Cars.Count > 1) Cars.Sort((car1, car2) => car1.PN.CompareTo(car2.PN));
		return false;
	}

	public void Update1()
	{
		UpdateTimeOffline();
		ListenToCars();
		UpdateLogList();
		CheckToDisqualify();
		UpdateCarsList();
	}
	void ListenToCars()
	{
		Cars = tp.ListenToCars(Cars);
	}

	void UpdateCarsList()
	{
		SBCars.Clear();
		SBCars.Append("                      === PILOTS DASHBOARD ===\n\n PN | TIRE | WEAR % | FUEL % | PITLANE | LIMITER | BOOST | ENG | DSQ\n");

		foreach (var car in Cars)
		{
			if (car == null || car.CurrentTire == null ) continue;
			SBCars.Append(" " + car.PN.ToString().PadRight(3).Substring(0, 3) +
				"| " + car.CurrentTire.PadRight(5) +
				"| " + (car.CurrentWear.ToString() + "%").PadRight(7) +
				"| " + (car.FuelPercent.ToString() + "%").PadRight(7) +
				"| " + (car.IsOnPitlane ? "PITLANE" : "").PadRight(8) +
				"| " + (car.IsSpeedLimited ? "LIMITED" : "").PadRight(8) +
				"| " + (car.IsBoostOn ? "BOOST" : "").PadRight(6) +
				"| " + (car.IsEngineOn ? " ON" : "OFF").PadRight(4) +
				"| " + (car.IsDisqualified ? "DSQ" : "").PadRight(3) + "\n");
		}
	}
	void UpdateLogList()
	{
		foreach (var car in Cars)
		{
			SBLog.Append(car.Log); car.Log = "";
		}
	}
	void CheckToDisqualify()
	{
		foreach(var car in Cars)
		{
			if (!car.IsDisqualified) {
				if (car.TimeOffline >= 3)
				{
					car.IsDisqualified = true; SBLog.Append(DateTime.Now.ToLongTimeString() + " | DSQ | " + car.PN + " | CONNECTION TIMEOUT\n");
				}
				else if (car.CurrentTire != car.OldTire)
				{
					car.IsDisqualified = true; SBLog.Append(DateTime.Now.ToLongTimeString() + " | DSQ | " + car.PN + " | " + car.CurrentTire + " != " + car.OldTire + " | TIRE CHANGED OUTSIDE THE PITLANE\n");
				}
				else if (car.OldWear > car.CurrentWear && !car.IsOnPitlane)
				{
					car.IsDisqualified = true; SBLog.Append(DateTime.Now.ToLongTimeString()+" | DSQ | " + car.PN + " | " + car.CurrentWear +" < " + car.OldWear + " | LESS WEAR OUTSIDE THE PITLANE\n");
				}
			}
		}
	}

	void UpdateTimeOffline()
	{
		foreach (var car in Cars)
		{
			car.TimeOffline += statisticsUpd;
		}
	}

	public string GetCarsList()
	{
		return SBCars.ToString();
	}

	public void UnDSQ(int carNum)
	{
		if (carNum == 0)
		{
			foreach (var car in Cars) car.IsDisqualified = false;
			SB.Request("Yes");
		}
		else
		{
			var existingCar = Cars.FirstOrDefault(x => x.PN == carNum);

			if (existingCar != null)
			{
				existingCar.IsDisqualified = false;
				SB.Request("Yes");
			}
		}
	}
	public string GetLogList()
	{
		return SBLog.ToString();
	}
	public void ClearLogList()
	{
		SBLog.Clear();
	}
}
class Car
{
	public int PN { get; set; }
	public string CurrentTire { get; set; }
	public string OldTire { get; set; }
	public double CurrentWear { get; set; }
	public double OldWear { get; set; }
	public bool IsOnPitlane { get; set; }
	public bool IsSpeedLimited { get; set; }
	public bool IsEngineOn { get; set; }
	public bool IsBoostOn { get; set; }
	public double FuelPercent { get; set; }
	public double TimeOffline { get; set; }
	public bool IsDisqualified { get; set; }
	public string Log { get; set; }
}
#endregion
#region Transponder
class Transponder
{
	IMyIntergridCommunicationSystem IGC;
	IMyBroadcastListener Reciever, ILSlistener;
	public string Channel = "DefaultCarRecieveChannel",BaseChannel = "DefaultBaseRecieveChannel", ILS_Channel = "Default_ILS", Callsign = "00", Callsign2 = "Default_Back", LastILS = "";
	
	public bool ILSChosen = false;
	StringBuilder SBFlightsList, SBTCAS;
	SoundBlock SB;
	public int Priority = 0;
	public double MaxDistToILSRunway = 10000;
	public MyIGCMessage Message, ILScords;

	public Transponder(IMyIntergridCommunicationSystem igc, string workmode, SoundBlock sb)
	{
		IGC = igc; SB = sb;
		if (workmode == "car")
		{
			Message = new MyIGCMessage();
			ILScords = new MyIGCMessage();
			Reciever = IGC.RegisterBroadcastListener(Channel);
			ILSlistener = IGC.RegisterBroadcastListener(ILS_Channel);
		}
		if (workmode == "base")
		{
			Message = new MyIGCMessage();
			Reciever = IGC.RegisterBroadcastListener(BaseChannel);
		}
		SBFlightsList = new StringBuilder();
		SBTCAS = new StringBuilder();
	}
	
	public List<Car> ListenToCars(List<Car> cars)
	{
		while (Reciever.HasPendingMessage)
		{
			Message = Reciever.AcceptMessage();
			string receivedData = Message.Data.ToString();
			string[] parsingRecieved = receivedData.Split('|');
			if (parsingRecieved.Length == 8)
			{
				int pn = int.Parse(parsingRecieved[0]);
				if (pn == 0) continue;
				string tire = parsingRecieved[1];
				double wear = double.Parse(parsingRecieved[2]), fuel = double.Parse(parsingRecieved[3]);
				bool pitlane = bool.Parse(parsingRecieved[4]), limiter = bool.Parse(parsingRecieved[5]),
					boost = bool.Parse(parsingRecieved[6]), engine = bool.Parse(parsingRecieved[7]);

				var existingCar = cars.FirstOrDefault(x => x.PN == pn);

				if (existingCar != null)
				{
					existingCar.OldTire = existingCar.CurrentTire;
					existingCar.CurrentTire = tire;
					existingCar.OldWear = existingCar.CurrentWear;
					existingCar.CurrentWear = wear;
					existingCar.FuelPercent = fuel;
					existingCar.IsOnPitlane = pitlane;
					existingCar.IsSpeedLimited = limiter;
					existingCar.IsBoostOn = boost;
					existingCar.IsEngineOn = engine;
					existingCar.TimeOffline = 0;
				}
				else
				{
					existingCar = new Car() { Log = "",CurrentTire = tire, OldTire = tire, PN = pn, CurrentWear = wear, OldWear = wear, FuelPercent = fuel, IsOnPitlane = pitlane,
					IsSpeedLimited = limiter, IsBoostOn = boost, IsEngineOn = engine, IsDisqualified = false, TimeOffline = 0};
					cars.Add(existingCar);
				}
			}
			else if (parsingRecieved.Length == 2)
			{
				int pn = int.Parse(parsingRecieved[0]);
				if (pn == 0) continue;
				string log = parsingRecieved[1];
				var existingCar = cars.FirstOrDefault(x => x.PN == pn);
				if (existingCar != null)
				{
					existingCar.Log += log;
				}
			}
		}
		List<Car> newCars = new List<Car>(cars);
		return newCars;
	}

	public string ListenToBase()
	{
		if (Reciever.HasPendingMessage)
		{
			Message = Reciever.AcceptMessage();
			return Message.Data.ToString();
		}
		else return "";
	}
	public void SendLogToBase(int pn, string log)
	{
		IGC.SendBroadcastMessage<string>(BaseChannel, pn + "|" + log, TransmissionDistance.AntennaRelay);
	}

	public void SendToBase(int pn, string tire, double wear, double fuel, bool pitlane, bool limiter, bool boost, bool engine)
	{
		IGC.SendBroadcastMessage<string>(BaseChannel, pn + "|" + tire + "|" + wear + "|" + fuel + "|" + pitlane + "|" + limiter + "|" + boost + "|" + engine, TransmissionDistance.AntennaRelay);
	}

	public void SendToCars(string num, string arg, string text)
	{
		IGC.SendBroadcastMessage<string>(Channel, num + "|" + arg + "|" + text, TransmissionDistance.AntennaRelay); SB.Request("Yes");
	}
}
#endregion
#region SoundBlock
class SoundBlock
{
	public bool SReady = false, LReady = false, TPReady = false, ServiceEnabled = true;
	Program Parent;
	List<IMySoundBlock> SBlocks;
	List<IMyLightingBlock> LBlocks;
	List<IMyTextPanel> TPBlocks;
	List<Sound> Queue; int CurTimes = 0; public double CurDelay = 0;
	public SoundBlock(Program parent, string sb_tag) { Parent = parent; TPBlocks = new List<IMyTextPanel>(); LBlocks = new List<IMyLightingBlock>(); SBlocks = new List<IMySoundBlock>(); Queue = new List<Sound>(); updateBlocks(sb_tag); }
	public void Request(string action)
	{
		if (!SReady && !LReady) return;
		switch (action)
		{
			case "BrakesOn": AddAction("SoundBlockAlert2", Color.LightGoldenrodYellow, 1, 0.10, 0.05); AddAction("SoundBlockAlert1", Color.Orange, 1, 0.10, 0.05); break;
			case "BrakesOff": AddAction("SoundBlockAlert1", Color.LightGoldenrodYellow, 1, 0.10, 0.05); AddAction("SoundBlockAlert2", Color.ForestGreen, 1, 0.10, 0.05); break;
			case "ObjComp": AddAction("SoundBlockObjectiveComplete", Color.LimeGreen, 1, 2, 2); break;
			case "Yes": AddAction("SoundBlockAlert2", Color.SpringGreen, 10, 0.05, 0.05); break;
			case "No": AddAction("SoundBlockAlert1", Color.OrangeRed, 10, 0.05, 0.05); break;
			case "YellowFlag": AddAction("MusComp_08", Color.Yellow, 4, 0.1, 0.1); break;
			case "BlueFlag": AddAction("MusComp_08", Color.Blue, 4, 0.1, 0.1); break;
			case "RedFlag": AddAction("MusComp_08", Color.Red, 4, 0.1, 0.1); break;
			case "BlackFlag": AddAction("MusComp_08", Color.Red, 4, 0.1, 0.1); break;
			default: break;
		}
	}
	void AddAction(string sound, Color col, int times, double playTime, double delay) { Sound element = new Sound() { Color = col, Delay = delay, PlayTime = playTime, SoundName = sound, Times = times }; Queue.Add(element); Sound element2 = new Sound() { Color = Color.Black, Delay = 0.1, PlayTime = 0.1, SoundName = "none", Times = 1 }; Queue.Add(element2); }
	public void update()
	{
		if ((!SReady && !LReady) || Queue.Count == 0) return;
		if (Play(Queue.First())) Queue.RemoveAt(0);
	}
	bool Play(Sound element)
	{
		if (CurTimes >= element.Times) { CurTimes = 0; CurDelay = 0.1; return true; }
		CurDelay = element.Delay;
		if (SReady) foreach (var S in SBlocks)
			{
				S.Stop();
				if (element.SoundName != "none")
				{
					S.SelectedSound = element.SoundName;
					S.LoopPeriod = (float)element.PlayTime;
					S.Play();
				}
			}
		if (LReady) foreach (var L in LBlocks)
			{
				L.Color = element.Color;
				if (element.SoundName != "none")
					L.BlinkIntervalSeconds = (float)element.PlayTime * 2;
				else L.BlinkIntervalSeconds = 0;
			}
		if(TPReady) foreach (var TP in TPBlocks)
			{
				TP.BackgroundColor = element.Color;
			}
		CurTimes++;
		return false;
	}
	public void updateBlocks(string sb_tag)
	{
		if (!ServiceEnabled) { SReady = LReady = false; return; }
		SBlocks.Clear(); Parent.GridTerminalSystem.GetBlocksOfType(SBlocks, x => x.CustomName.Contains(sb_tag));
		SReady = SBlocks.Count() > 0;
		LBlocks.Clear(); Parent.GridTerminalSystem.GetBlocksOfType(LBlocks, x => x.CustomName.Contains(sb_tag));
		LReady = LBlocks.Count() > 0;
		TPBlocks.Clear(); Parent.GridTerminalSystem.GetBlocksOfType(TPBlocks, x => x.CustomName.Contains(sb_tag));
		TPReady = TPBlocks.Count() > 0;
		if (LReady) foreach (var L in LBlocks) L.BlinkLength = 50;
		if(TPReady) foreach (var light in TPBlocks)
		{
			light.ContentType = ContentType.TEXT_AND_IMAGE;
			light.FontColor = Color.Black;
		}
	}
	class Sound
	{
		public string SoundName { get; set; }
		public int Times { get; set; }
		public double PlayTime { get; set; }
		public double Delay { get; set; }
		public Color Color { get; set; }
	}
}
#endregion
class Controller
{
	#region APGlobal
	Program Parent;
	StringBuilder SBCar, SBNav, SBTires, SBErrors;
	public int pN = 0;

	Vector3D MyPos, MyVel, GravityVector;

	bool speedRecorded = false, speedLimited = false, powerRecorded = false, powerLimited = false;
	Vector3D pitLaneVector = Vector3D.Zero, normalizedPitLaneVector = Vector3D.Zero;
	public Vector3D MyBase = new Vector3D(0, 0, 0), pitLaneStart = new Vector3D(0, 0, 0), pitLaneEnd = new Vector3D(0, 0, 0);
	public double pitLaneTreshold = 15, pitLaneMinusTreshold = 0, baseTreshold = 5;
	public double speedLimit = 80, userSpeed = 360, powerLimit = 5, userPower = 90, maxPower = 90, boostPower = 100;
	double distanceToPitLane = 0, distanceToBase = 0, pitlaneDotProduct = 0;

	int fuelInEngines = 0; double fuel = 0, maxFuel = 0, fuelAvrEMA = 0, fuelEMA_A = 0.05, oldFuel = 0; string fuelRemainingTime = "";

	public bool engineWorking = false, onPitLane = false, lastEngineWorking = false, lastOnPitlane = false;

	double MyVelHor = 0, MyOldVelHor = 0, MyHorAcceleration = 0;
	double Forward, Right, Down;

	public List<Tire> tireSets;
	public Tire CurrentTire; 
	TireType requestedType = TireType.Hard;
	double requestedWear = 1.0;
	int requesteedIndex = -1;
	List<int> requestedIndexes;
	bool tireRequested = false;
	public string tireShowList = "", password = "0";

	public double VRotate = 80, V2 = 100, MinLandVelocity = 60, MaxLandVelocity = 80, AirbrakeVelocity = 90, BasicLandingAngle = 3, Sealevel_Calibrate = 0;
	public double MaxPitchAngle = 45, MaxRollAngle = 45, PitchSpeedMultiplier = 1, YawSpeedMultiplier = 1, RollSpeedMultiplier = 1, MaxPitchSpeed = 15, MaxRollSpeed = 15, MaxYawSpeed = 5;
	

	public int BasicAPSpeed = 100, BasicAPAltitude = 2500, BasicWaitTime = 60; //m/s, m, sec
	public string BasicILSCallsign = "default", AutopilotTimerName = "default";
	public double CruiseSpeed = 0, CruiseAltitude = 0;


	public string string_status = "None", RCName = "", WBName = "";
	public int WaypointNum = 9999, CruiseWNum = 9999, lastWaypointNum = 0, TickCounter = 0;
	public bool RepeatRoute = true, ApEngaged = false, CruiseEngaged = false, UseTCAS = true, UseLandTCAS = true, UseGPWS = true, UseLandGPWS = true, UseDamp = true, AlwaysDampiners = false, RCReady;

	MyIni Ini;
	IMyGridProgramRuntimeInfo Runtime;
	IMyProgrammableBlock Me;
	IMyShipController UnderControl; Vector3 MoveInput = Vector3.Zero; Vector2 RotationInput = Vector2.Zero; double WS = 0, AD = 0, SpaceC = 0, QE = 0, PitchInput = 0;
	public IMyShipController RC;
	List<MyDetectedEntityInfo> detectedEntities;
	public IMySensorBlock Sensor; public bool SensorReady = false, sensorBoostAllowed = true, useSensorBoost = true, boostEngaged = false;
	float sensorUp = 0.5f, sensorDown = 0.5f, sensorLeft = 1, sensorRight = 1, sensorFront = 30, sensorBack = 0.1f;
	public double boostEngageTime = 7.5, timeSinceBoostEngaged = 0, boostDropAcceleration = -3, sensorBoostSpeedTreshold = 35;
	List<IMyShipController> controllers;
	public List<IMyMotorSuspension> wheels;
	public List<IMyGasTank> gasTanks;
	public List<IMyPowerProducer> engines;
	public List<IMyLightingBlock> signalLights; public bool signalReady = false;
	List<IMySensorBlock> sensors;
	
	Transponder tp;
	SoundBlock SB;
	public Controller(Program parent, string controllerName, string waypointsBlockName, string IncludeTag, IMyProgrammableBlock me, MyIni ini, IMyGridProgramRuntimeInfo runtime, Transponder Trans, SoundBlock sb)
	{
		Parent = parent; Me = me; Ini = ini; Runtime = runtime; tp = Trans; SB = sb;
		RCName = controllerName; WBName = waypointsBlockName;
		
		SBCar = new StringBuilder(); SBNav = new StringBuilder(); SBTires = new StringBuilder(); SBErrors = new StringBuilder("\n");
		controllers = new List<IMyShipController>(); wheels = new List<IMyMotorSuspension>();
		engines = new List<IMyPowerProducer>();
		gasTanks = new List<IMyGasTank>();
		signalLights = new List<IMyLightingBlock>();
		sensors = new List<IMySensorBlock>();
		detectedEntities = new List<MyDetectedEntityInfo>();
		tireSets = new List<Tire>();
		requestedIndexes = new List<int>();

		CurrentTire = Tire.SetTire(TireType.Hard);
		CurrentTire.SetWear(0);

		UpdateBlocks();
	}
	#endregion

	#region update
	public void UpdateBlocks()
	{
		controllers.Clear(); wheels.Clear(); signalLights.Clear(); engines.Clear(); gasTanks.Clear(); sensors.Clear(); TickCounter = 0;
		Parent.GridTerminalSystem.GetBlocksOfType(controllers, x => x.CanControlShip && x.IsSameConstructAs(Me));
		if (controllers.Count > 0) { RC = controllers.First(); RCReady = RC != null; } else { RCReady = false; return; }
		Parent.GridTerminalSystem.GetBlocksOfType(wheels, x => x.IsSameConstructAs(RC) );
				//&& (x.WorldMatrix.Left == RC.WorldMatrix.Forward || x.WorldMatrix.Right == RC.WorldMatrix.Forward)
				//&& x.WorldMatrix.Up == RC.WorldMatrix.Up 
		Parent.GridTerminalSystem.GetBlocksOfType(engines, x => x.BlockDefinition.TypeId.ToString() == "MyObjectBuilder_HydrogenEngine" && x.IsSameConstructAs(RC));
		Parent.GridTerminalSystem.GetBlocksOfType(gasTanks, x =>
				(x.BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/LargeHydrogenTank" ||
				x.BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/LargeHydrogenTankIndustrial" ||
				x.BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/LargeHydrogenTankSmall" ||
				x.BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/SmallHydrogenTank" ||
				x.BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/SmallHydrogenTankSmall")
				&& x.IsSameConstructAs(RC));
		Parent.GridTerminalSystem.GetBlocksOfType(signalLights, x => x.CustomName.Contains(SL_TAG) && x.IsSameConstructAs(RC));
		signalReady = signalLights.Count > 0;


		Parent.GridTerminalSystem.GetBlocksOfType(sensors, x => x.WorldMatrix.Forward == RC.WorldMatrix.Forward && x.WorldMatrix.Up == RC.WorldMatrix.Up && x.CustomName.Contains(Sensor_TAG) && x.IsSameConstructAs(RC));
		if (sensors.Count > 0) Sensor = sensors.First(); SensorReady = Sensor != null;
		maxFuel = 0; foreach (var tank in gasTanks) maxFuel += tank.Capacity; maxFuel += engines.Count * 5000;
		if (SensorReady) {
		BoundingBoxD gridBoundingBox = RC.CubeGrid.WorldAABB;
		double sensorPosition = Vector3D.Dot(Sensor.WorldMatrix.Translation - gridBoundingBox.Center, gridBoundingBox.HalfExtents) / gridBoundingBox.HalfExtents.LengthSquared();
		sensorLeft = 2;//(float)Clamp(Math.Max(sensorPosition, 0.1), 0, 10);
		sensorRight = 2;//(float)Clamp(Math.Max(1 - sensorPosition, 0.1), 0, 10);
		}
	}
	public bool Update()
	{
		if (!RCReady || RC.Closed) return false;
		TickCounter++;
		GravityVector = Vector3D.Normalize(RC.GetNaturalGravity());
		if (double.IsNaN(GravityVector.LengthSquared())) return false;
		//GetAltitude();
		
		MyPos = RC.GetPosition();
		MyVel = RC.GetShipVelocities().LinearVelocity;

		Forward = MyVel.Dot(RC.WorldMatrix.Forward);
		Right = MyVel.Dot(RC.WorldMatrix.Right);
		Down = MyVel.Dot(RC.WorldMatrix.Down);

		UpdateMoveInput();
		UpdateLights();

		//MyVelVert = R(-MyVel.Dot(GravityVector), 2);
		MyVelHor = R(Vector3D.Reject(MyVel, GravityVector).Length(), 2);
		UpdateAcceleration();

		engineWorking = CheckEngineWorking();
		onPitLane = CheckDistanceToPitLane();
		CalculateFuel();
		//CalculateHeading();
		//Parent.Echo("Tanks: " + gasTanks.Count.ToString());

				

		if (!engineWorking)
		{
			if (boostEngaged)
			{
				BoostMaxPower(false);
				boostEngaged = false;
				tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / BOOST OFF / " + pN + " / DISENGAGE BOOST DUE TO ENGINE OFF\n"));
			}
			LimitMaxPower(true);
			powerLimited = true;
			if(lastEngineWorking != engineWorking)
			{
				lastEngineWorking = engineWorking;
				tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / POWER LIMIT / " + pN + " / ENGAGE POWER LIMIT DUE TO ENGINE OFF\n"));
			}
		}
		else if (powerLimited) { LimitMaxPower(false); powerLimited = false; lastEngineWorking = engineWorking; tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / POWER UNLIMIT / " + pN + " / DISENGAGE POWER LIMIT DUE TO ENGINE ON\n"));}


		if (onPitLane)
		{
			if (boostEngaged)
			{
				BoostMaxPower(false);
				boostEngaged = false;
				tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / BOOST OFF / " + pN + " / DISENGAGE BOOST DUE TO ENTERING PITLANE\n"));
			}
			LimitMaxSpeed(true);
			speedLimited = true;
			if(lastOnPitlane != onPitLane)
			{
				lastOnPitlane = onPitLane;
				tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / SPEED LIMIT / " + pN + " / ENGAGE SPEED LIMIT DUE TO ENTERING PITLANE\n"));
			}
		}
		else if(speedLimited) { LimitMaxSpeed(false); speedLimited = false; lastOnPitlane = onPitLane; tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / SPEED UNLIMIT / " + pN + " / DISENGAGE SPEED LIMIT DUE TO LEAVING PITLANE\n")); }

		
		if (CheckDistanceToBase() && tireRequested && MyVelHor < 0.5)
		{
			SetTireOnBase();
		}

		if (!powerLimited && !onPitLane)
		{
			if (CheckSensorBoost()) { BoostMaxPower(true); timeSinceBoostEngaged = 0; boostEngaged = true; }
			else if(boostEngaged && (timeSinceBoostEngaged >= boostEngageTime || MyHorAcceleration <= boostDropAcceleration)) { BoostMaxPower(false); boostEngaged = false; }
		}
		else if (boostEngaged) { BoostMaxPower(false); boostEngaged = false; }


		if (!boostEngaged && !powerLimited)
		{
			if (CheckUpperPower(maxPower)) { tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / POWER LIMIT / " + pN + " / OUTSIDE INTERFERENCE! TRYING TO RETURN POWER LIMIT TO NORMAL \n")); foreach (var wheel in wheels) wheel.Power = (float)maxPower; }
		}

		CurrentTire.UpdateWear(Math.Abs(MyHorAcceleration), Math.Abs(Forward), Math.Abs(Right));

		SetFriction(CurrentTire.Friction);

		
		
			
		
		updateTiresList();
		updateCarList();
		updateNavList();
		return false;
	}

	public void Update1()
	{
		tp.SendToBase(pN, GetTireType(CurrentTire.Type), R(CurrentTire.Wear * 100, 1), R((fuel / maxFuel) * 100, 1), onPitLane, speedLimited, boostEngaged, engineWorking);
		ListenToBase();
	}
	public string CreateSaveLog(bool onOff)
	{
		return (DateTime.Now.ToLongTimeString() + " //// CONTROLLER " + (onOff ? "ON" : "OFF") + " //// " + pN +
		" / " + CurrentTire.Type + " : " + CurrentTire.Wear + " / FUEL: " + R((fuel / maxFuel) * 100, 3) + " / PIT " + onPitLane +
		" / SPD LMT " + speedLimited + " / BOOST " + boostEngaged + " / ENG " + engineWorking + " / POW LMT " + powerLimited + " / PB ON? " + Parent.Me.Enabled + "\n");
	}

	public void ListenToBase()
	{
		string receivedData = tp.ListenToBase();
		string[] parsingRecieved = receivedData.Split('|');
		if (parsingRecieved.Length == 3)
		{
			int pn = 0; int.TryParse(parsingRecieved[0], out pn);

			if (pn == pN || pn == 0)
			{
				bool success = false;
				switch (parsingRecieved[1].ToLower())
				{
					case "mybase": MyBase = Parent.ParsePoint(parsingRecieved[2]); success = true; break;
					case "pitlane": string[] cords = parsingRecieved[2].Split(';'); if (cords.Length == 2) { pitLaneStart = Parent.ParsePoint(cords[0]); pitLaneEnd = Parent.ParsePoint(cords[1]); success = true; } break;
					case "basetreshold": success = double.TryParse(parsingRecieved[2], out baseTreshold); break;
					case "pitlanetreshold": success = double.TryParse(parsingRecieved[2], out pitLaneTreshold); break;
					case "speedlimit": success = double.TryParse(parsingRecieved[2], out speedLimit); break;
					case "sensorboost": success = bool.TryParse(parsingRecieved[2], out sensorBoostAllowed); break;
					case "sesorboostspeedtreshold": success = double.TryParse(parsingRecieved[2], out sensorBoostSpeedTreshold); break;
					case "boostengagetime": success = double.TryParse(parsingRecieved[2], out boostEngageTime); break;
					case "tire": string[] tire = parsingRecieved[2].Split(':'); if (tire.Length == 2) { double wear = 0; double.TryParse(tire[1], out wear); SetTireFromStorage(tire[0], wear); success = true; } break;
					case "tiresets": success = Parent.ParseTiresList(parsingRecieved[2]); break;
					case "password": password = parsingRecieved[2]; success = true; break;
					case "addtire": string[] tire2 = parsingRecieved[2].Split(':'); if (tire2.Length == 2) success = AddTireToSets(tire2[0], tire2[1]); break;
					case "remove": success = RemoveMostWornTire(parsingRecieved[2]); break;
					default: break;
				}
				if (success) SB.Request("Yes"); else SB.Request("No");
				Parent.SaveStorage();
			}
		}
	}
	bool AddTireToSets(string type, string wear)
	{
		TireType tire = TireType.Hard;
		double Wear = 0;
		bool success = Enum.TryParse(type, out tire) && double.TryParse(wear, out Wear);
		if (success)
		{
			Tire newTire = Tire.SetTire(tire);
			newTire.SetWear(Wear);
			newTire.UpdateFriction();
			tireSets.Add(newTire);
			return true;
		}
		else return false;
	}
	bool RemoveMostWornTire(string type)
	{
		Tire mostWornTire = null;
		double maxWear = -1;
		TireType tireType;
		bool success = Enum.TryParse(type, out tireType);
		if (success)
		{
			foreach (var tire in tireSets)
			{
				if (tire.Type == tireType && tire.Wear > maxWear)
				{
					mostWornTire = tire;
					maxWear = tire.Wear;
				}
			}

			if (mostWornTire != null)
			{
				tireSets.Remove(mostWornTire);
				return true;
			}
			else return false;
		}
		else return false;
	}
	#endregion
	#region route switch
	
	void TriggerRouteTimer(string blockname)
	{
		if (blockname == "default") return;
		List<IMyTimerBlock> Blocks = new List<IMyTimerBlock>();
		Parent.GridTerminalSystem.GetBlocksOfType(Blocks, x => x.CustomName.Contains(blockname));
		IMyTimerBlock Block;
		if (Blocks.Count > 0) Block = Blocks.First(); else { SBErrors.Append("\n " + blockname + " Not Found!"); return; }
		Block.Trigger();
	}
	#endregion

	#region Tires List

	void updateTiresList()
	{
		SBTires.Clear();
		SBTires.Append("     === TIRES ===\n");
		SBTires.Append(" TYPE [" + GetTireType(CurrentTire.Type) + "]\n");
		SBTires.Append(" INTEGRITY [" + R((1 - CurrentTire.Wear) * 100, 2) + "%]\n");
		SBTires.Append(" FRICTION [" + R(CurrentTire.Friction, 2) + "%]\n");
		SBTires.Append(" EST. TIME [" + CurrentTire.CalcTimeLeft() + "]\n");
		if(tireRequested) SBTires.Append(" CHANGE [" + GetTireType(requestedType) + '(' + R((1 - requestedWear) * 100, 1) +')'+ "]\n");
		SBTires.Append(" SETS:" + tireShowList);
	}

	string GetTireType(TireType currentTire)
	{
		string type = "";
		switch (currentTire)
		{
			case TireType.Ultrasoft : type = "ULTR"; break;
			case TireType.Supersoft: type = "SUPR"; break;
			case TireType.Soft: type = "SOFT"; break;
			case TireType.Medium: type = "MED"; break;
			case TireType.Hard: type = "HARD"; break;
			
			default: break;
		}
		return type;
	}

	public string GetTiresList()
	{
		return SBTires.ToString();
	}

	#endregion

	#region Car list
	void updateCarList()
	{
		SBCar.Clear();
		SBCar.Append("   === DASHBOARD ===\n");
		SBCar.Append(" VELOCITY   ACCEL\n");
		SBCar.Append((" [" + MyVelHor + "m/s]").PadRight(12) + "[" + MyHorAcceleration + "m/s²]\n");
		SBCar.Append(" BOOST      LIMITER\n");
		SBCar.Append((boostEngaged? " [ BOOST ]" : " [       ]").PadRight(12) + (speedLimited ? "[LIMITED]\n" : "[       ]\n"));
		SBCar.Append(" FUEL       ENGINE\n");
		SBCar.Append((" [" + fuelRemainingTime + "]").PadRight(12) + (engineWorking ? "[ON]\n" : "[OFF]\n"));
	}

	public string GetCarList()
	{
		return SBCar.ToString();
	}
	#endregion
	#region Navigation list
	void updateNavList()
	{
		SBNav.Clear();
		SBNav.Append("   === NAVIGATION ===\n");

		SBNav.Append(" TO PITLANE [" + (distanceToPitLane) + "]\n");
		SBNav.Append(" TO BASE [" + (distanceToBase) + "]\n");

	}

	public string GetNav()
	{
		return SBNav.ToString();
	}
	#endregion

	#region Fuel

	bool CheckEngineWorking()
	{
		fuelInEngines = 0;
		bool isWorking = true;
		if (engines.Count > 0)
			foreach (var gen in engines)
			{
				int fuelAmount = GetHydrogenAmount(gen.DetailedInfo);

				if (fuelAmount == 0 || !gen.Enabled) isWorking = false;

				fuelInEngines += fuelAmount;
			}
		return isWorking;
	}

	public int GetHydrogenAmount(string detailedInfo)
	{
		// Ищем индекс начала значения
		int startIndex = detailedInfo.IndexOf("(") + 1;

		// Ищем индекс конца значения
		int endIndex = detailedInfo.IndexOf("/") - 1;

		// Вырезаем значение из строки
				
		return int.Parse(detailedInfo.Substring(startIndex, endIndex - startIndex));
	}

	void CalculateFuel()
	{
		fuel = 0;
		foreach (var tank in gasTanks) fuel += tank.FilledRatio * tank.Capacity;
		fuel += fuelInEngines;
		if (MyVelHor > 5) {
		double difference = oldFuel - fuel;
		oldFuel = fuel;
		if (difference < 0) return;

		fuelAvrEMA = fuelEMA_A * difference + (1 - fuelEMA_A) * fuelAvrEMA;

		double remainingTime = fuel / (fuelAvrEMA / timeLimit);

		fuelRemainingTime = TimeSpan.FromSeconds(Math.Min(remainingTime, 35999)).ToString(@"h\:mm\:ss");
		}
	}


	#endregion

	#region Tires
	#region mdk preserve
	public enum TireType{Ultrasoft,Soft,Medium,Hard,Supersoft}
	#endregion
	public class Tire
	{
		public TireType Type { get; private set; }
		public float Friction { get; private set; }
		public float MinFriction { get; private set; }
		public float MaxFriction { get; private set; }
		public float WearRate { get; private set; }
		public double Wear { get; private set; }
		public double remainingTime { get; private set; }
		public float FrictionLimit { get; private set; }
		public double EMA_A { get; private set; }
		public double AvrEMA { get; private set; }

		private static Dictionary<TireType, Tire> tireTypes = new Dictionary<TireType, Tire>
		{
			{ TireType.Ultrasoft, new Tire(TireType.Ultrasoft, 30f, 100f, 9.66f) },
			{ TireType.Supersoft, new Tire(TireType.Supersoft, 35f, 90f, 5.33f) },
			{ TireType.Soft, new Tire(TireType.Soft, 40f, 80f, 2.66f) },
			{ TireType.Medium, new Tire(TireType.Medium, 42.5f, 65f, 1.40f) },
			{ TireType.Hard, new Tire(TireType.Hard, 45f, 50f, 0.99f) }
		};

		private Tire(TireType type, float minFriction, float maxFriction, float wearRate)
		{
			Type = type;
			MinFriction = minFriction;
			MaxFriction = maxFriction;
			WearRate = wearRate;
			Wear = 0;
			remainingTime = 0;
			EMA_A = 0.01;
			AvrEMA = 0;
			FrictionLimit = 20f;
			Friction = 20f;
		}

		public static Tire SetTire(TireType type)
		{
			Tire selectedTire;
			return tireTypes.TryGetValue(type, out selectedTire) ? new Tire(selectedTire) : new Tire(TireType.Hard, 45f, 50f, 0.85f);
		}

		private Tire(Tire source)
		{
			Type = source.Type;
			MinFriction = source.MinFriction;
			MaxFriction = source.MaxFriction;
			WearRate = source.WearRate;
			Wear = source.Wear;
			remainingTime = source.remainingTime;
			EMA_A = source.EMA_A;
			AvrEMA = source.AvrEMA;
			FrictionLimit = source.FrictionLimit;
			Friction = source.Friction;
		}
		public void SetWear(double wear) { Wear = wear; }
		public void UpdateWear(double acceleration, double forward, double right)
		{
			if (forward < 5 && right < 5) return;
			double currentWear = (WearRate * (acceleration + forward * 0.1 + right * 0.5) * timeLimit) * 0.0001;
			Wear += currentWear;
			Wear = MathHelper.Clamp(Math.Round(Wear, 6), 0, 1);
			AvrEMA = EMA_A * currentWear + (1 - EMA_A) * AvrEMA;
			remainingTime = (1 - Wear) / (AvrEMA / timeLimit);
			UpdateFriction();
		}
		public void UpdateFriction() { Friction = Wear < 1.0 ? (float)(MinFriction + (MaxFriction - MinFriction) * (1 - Math.Exp(6 * (Wear - 1)))) : FrictionLimit; }
		
		public string CalcTimeLeft() => TimeSpan.FromSeconds(Math.Min(remainingTime, 35999)).ToString(@"h\:mm\:ss");
	}

	void SetFriction(float friction)
	{
		if (!CheckFriction(friction)) tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / FRICTION / " + pN + " / " + wheels.FirstOrDefault().Friction + " != " + friction + " / FRICTION CHANGED TOO QUICKLY \n"));
		foreach (var wheel in wheels) wheel.Friction = friction;
	}
	bool CheckFriction(double friction)
	{
		if (distanceToBase < baseTreshold) return true;
		foreach (var wheel in wheels)
		if (Math.Abs(wheel.Friction - friction) > 1)
		{
			return false;
		}
		return true;
	}

	public void RequestTire(TireType type)
	{
		for(int i = 0; i < tireSets.Count; i++)
		{
			if (tireSets[i].Type == type && tireSets[i].Wear < 0.95)
			{
				if (requestedIndexes.Contains(i)) continue;

				requestedWear = tireSets[i].Wear;
				requesteedIndex = i;
				requestedIndexes.Add(i);
				requestedType = type;
				tireRequested = true;
				SB.Request("Yes");
				return;
			}
		}
		AbortTireRequest();
	}
	void SetTireOnBase()
	{
		tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / TIRE CHANGE / "  + pN + " / " + CurrentTire.Type + ':' + CurrentTire.Wear + " >>> " + requestedType + ':' + requestedWear + "\n"));
		tireSets.Add(CurrentTire);

		CurrentTire = Tire.SetTire(requestedType);
		CurrentTire.SetWear(requestedWear);

		tireSets.RemoveAt(requesteedIndex);
		Parent.SaveStorage();

		requesteedIndex = -1;
		requestedIndexes.Clear();
		requestedWear = 0;
		tireRequested = false;

		CurrentTire.UpdateFriction();
		SB.Request("ObjComp");
	}
	public void SetTireFromStorage(string type, double wear)
	{
		TireType tire = TireType.Hard;
		bool success = Enum.TryParse(type, out tire);
		if (success)
		{
			CurrentTire = Tire.SetTire(tire);
			CurrentTire.SetWear(wear);
			CurrentTire.UpdateFriction();
		}
		else SB.Request("No");
	}

	public void AbortTireRequest()
	{
		requesteedIndex = -1;
		requestedIndexes.Clear();
		requestedWear = 0;
		tireRequested = false;
		 SB.Request("No");
	}

	public void UpdateTiresList()
	{
		tireShowList = "";
		List<Tire> list = new List<Tire>();
		foreach(var set in tireSets) { if(set.Wear > 0.95) continue; list.Add(set); }
		for (int i = 0; i < list.Count; i++)
		{
			if (i == 0)
			{
				tireShowList += " [" + GetTireType(list[i].Type) + "(" + R((1 - list[i].Wear) * 100, 1) + ")]";
			}
			else
			{
				tireShowList += (i % 2 == 1) ? " [" : ",";
				tireShowList += GetTireType(list[i].Type) + "(" + R((1 - list[i].Wear) * 100, 1) + ")";
				if (i % 2 == 0 || (i % 2 == 1 && i == list.Count - 1))
				{
					tireShowList += "]";
				}
			}
			if (i % 2 == 0)
			{
				tireShowList += "\n";
			}
		}
	}


	#endregion

	#region Lights
	void UpdateLights()
	{
		
		if ((Forward > 0.4 && MyHorAcceleration < 0 && WS >= 0) || SpaceC > 0)
		{
			foreach (var light in signalLights)
			{
				light.Radius = 2f;
				light.Intensity = 5f;
				light.Falloff = 1.3f;
				light.Color = Color.Red;
			}
			
		}
		else if (Forward < -0.5)
		{
			foreach (var light in signalLights)
			{
				light.Radius = 5f;
				light.Intensity = 5f;
				light.Falloff = 1.3f;
				light.Color = Color.White;
			}
			
		}
		else
		{
			foreach (var light in signalLights)
			{
				light.Radius = 1f;
				light.Intensity = 1f;
				light.Falloff = 0;
				light.Color = Color.DarkRed;
			}
			
		}
		
	}
			#endregion

	#region Boost
	bool CheckSensorBoost()
	{
		if (!SensorReady || !sensorBoostAllowed || !useSensorBoost) return false;
		Sensor.FrontExtend = sensorFront;
		Sensor.BackExtend = sensorBack;
		Sensor.TopExtend = sensorUp;
		Sensor.BottomExtend = sensorDown;
		Sensor.LeftExtend = sensorLeft;
		Sensor.RightExtend = sensorRight;

		Sensor.DetectAsteroids = false;
		Sensor.DetectEnemy = true;
		Sensor.DetectFriendly = true;
		Sensor.DetectFloatingObjects = false;
		Sensor.DetectLargeShips = false;
		Sensor.DetectNeutral = true;
		Sensor.DetectOwner = true;
		Sensor.DetectPlayers = false;
		Sensor.DetectSmallShips = true;
		Sensor.DetectStations = false;
		Sensor.DetectSubgrids = false;

		detectedEntities.Clear();
		Sensor.DetectedEntities(detectedEntities);
		//Parent.Echo("detected: " +detectedEntities.Count);
		if (detectedEntities.Count == 0) return false;

		MyDetectedEntityInfo otherCar = new MyDetectedEntityInfo();

		if (detectedEntities.Count > 0) otherCar = detectedEntities.First(x => x.Type == MyDetectedEntityType.SmallGrid);
		//Parent.Echo("Other Car: " + otherCar.Name);
		if (otherCar.IsEmpty()) return false;

		if (otherCar.Velocity.LengthSquared() >= sensorBoostSpeedTreshold * sensorBoostSpeedTreshold
			&& MyVelHor >= sensorBoostSpeedTreshold) return true;
		//Parent.Echo("Not found but good");
		return false;
	}
	#endregion

	#region MoveInput
	void UpdateMoveInput()
	{
		UnderControl = GetControlledShipController();
		MoveInput = UnderControl.MoveIndicator;
		WS = MoveInput.Z;
		SpaceC = MoveInput.Y;
		AD = MoveInput.X;
	}
	#endregion

	#region Speed Control
	public void UpdateAcceleration()
	{
		MyHorAcceleration = R((MyVelHor - MyOldVelHor) / timeLimit, 1);
		MyOldVelHor = MyVelHor;
	}

	public bool CheckDistanceToPitLane()
	{
		if(pitLaneVector.IsZero()) return false;

		Vector3D distToPitLaneStart = MyPos - pitLaneStart;

		double projection = Vector3D.Dot(distToPitLaneStart, normalizedPitLaneVector);
		Vector3D closestPointOnPitlane = pitLaneStart + Clamp(projection, pitLaneTreshold, pitLaneMinusTreshold) * normalizedPitLaneVector;

		distanceToPitLane = Math.Round(Vector3D.Distance(MyPos, closestPointOnPitlane), 1);

		return distanceToPitLane < pitLaneTreshold;
	}

	public bool CheckDistanceToBase()
	{
		if (MyBase.IsZero()) return false;
		distanceToBase = Math.Round(Vector3D.Distance(MyBase, MyPos),1);
		return distanceToBase < baseTreshold;
	}

	public void CreatePitLaneProperties()
	{
		if (pitLaneStart.IsZero() || pitLaneEnd.IsZero()) return;
		pitLaneVector = pitLaneEnd - pitLaneStart;
		normalizedPitLaneVector = Vector3D.Normalize(pitLaneVector);
		if (pitLaneVector.Length() < pitLaneTreshold * 2) pitLaneTreshold = (pitLaneVector.Length() - 1)  / 2;
		pitLaneMinusTreshold = pitLaneVector.Length() - pitLaneTreshold;
	}

	void LimitMaxSpeed(bool condition)
	{
		if (CheckMaxSpeed(condition) == condition) return;
		if (condition) tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / SPEED LIMIT / " + pN + " / TRYING TO LIMIT MAX SPEED\n"));
		else tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / SPEED LIMIT / " + pN + " / TRYING TO RETURN MAX SPEED\n"));
		if (condition && !speedRecorded) { userSpeed = wheels.First().GetValueFloat("Speed Limit"); speedRecorded = true; }
		else if (!condition && speedRecorded) speedRecorded = false;

		foreach (var block in wheels) block.SetValueFloat("Speed Limit", condition ? (float)speedLimit : (float)userSpeed);
		
		if (condition) SB.Request("BrakesOn"); else SB.Request("BrakesOff");
	}

	bool CheckMaxSpeed(bool condition)
	{
		foreach (var wheel in wheels)
		if (condition ? (wheel.GetValueFloat("Speed Limit") != speedLimit) : (wheel.GetValueFloat("Speed Limit") == speedLimit)) return !condition; return condition;
	}

	void LimitMaxPower(bool condition)
	{
		if (CheckMaxPower(condition, powerLimit) == condition) return;
		if (condition) tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / POWER LIMIT / " + pN + " / TRYING TO LIMIT POWER DUE TO ENGINE OFF\n"));
		else tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / POWER LIMIT / " + pN + " / TRYING TO RETURN POWER LIMIT DUE ENGINE ON\n"));
		if (condition && !powerRecorded) { userPower = wheels.First().Power; powerRecorded = true; }
		else if (!condition && powerRecorded) powerRecorded = false;

		foreach (var block in wheels) block.Power = condition ? (float)powerLimit : (float)userPower;
		
		if (condition) SB.Request("No"); else SB.Request("Yes");
	}

	void BoostMaxPower(bool condition)
	{
		if (CheckMaxPower(condition, boostPower) == condition) return;
		if (condition) tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / POWER LIMIT / " + pN + " / TRYING TO SET POWER LIMIT TO BOOST \n"));
		else tp.SendLogToBase(pN, (DateTime.Now.ToLongTimeString() + " / POWER LIMIT / " + pN + " / TRYING TO RETURN POWER LIMIT TO NORMAL \n"));
		if (condition && !powerRecorded) { userPower = wheels.First().Power; powerRecorded = true; }
		else if (!condition && powerRecorded) powerRecorded = false;

		foreach (var block in wheels) block.Power = condition ? (float)boostPower : (float)userPower;
	}

	bool CheckMaxPower(bool condition, double PowerLimit)
	{
		foreach (var wheel in wheels)
			if (condition ? (wheel.Power != PowerLimit) : (wheel.Power == PowerLimit))
			{
				return !condition;
			}
		return condition;
	}
	bool CheckUpperPower(double PowerLimit)
	{
		foreach (var wheel in wheels)
			if (wheel.Power > PowerLimit) return true; return false;
	}

	#endregion

	#region Tools
	//Abbreviated functions to save characters. Not so necessary anymore, since I use the minifier
	double Clamp(double a, double b, double c) { return MathHelper.Clamp(a, b, c); }
	double ToR(double a) { return MathHelper.ToRadians(a); }
	double R(double a, int b = 0) { return Math.Round(a, b); }
	IMyShipController GetControlledShipController()
	{
		if (controllers.Count == 0) return null;
		foreach (IMyShipController c in controllers) { if (c.IsUnderControl && c.CanControlShip) return c; }
		return RC;
	}
	public void ClearErrorList() { SBErrors.Clear(); SBErrors.Append("\n"); }
	public bool ErrListNotEmpty() { if (SBErrors.Length > 1) return true; else return false; }
	public string GetErrorsList() { return SBErrors.ToString(); }
	#endregion
	#region PID
	class PID
	{
		double _kP = 0, _kI = 0, _kD = 0, _integralDecayRatio = 0, _lowerBound = 0, _upperBound = 0, _timeStep = 0, _inverseTimeStep = 0, _errorSum = 0, _lastError = 0;
		bool _firstRun = true, _integralDecay = false;
		public double Value { get; private set; }
		public PID(double kP, double kI, double kD, double lowerBound, double upperBound, double timeStep)
		{ _kP = kP; _kI = kI; _kD = kD; _lowerBound = lowerBound; _upperBound = upperBound; _timeStep = timeStep; _inverseTimeStep = 1 / _timeStep; _integralDecay = false; }
		public PID(double kP, double kI, double kD, double integralDecayRatio, double timeStep)
		{ _kP = kP; _kI = kI; _kD = kD; _timeStep = timeStep; _inverseTimeStep = 1 / _timeStep; _integralDecayRatio = integralDecayRatio; _integralDecay = true; }
		public double Control(double error)
		{
			//Compute derivative term
			var errorDerivative = (error - _lastError) * _inverseTimeStep;
			if (_firstRun) { errorDerivative = 0; _firstRun = false; }
			//Compute integral term
			if (!_integralDecay) { _errorSum += error * _timeStep; _errorSum = MathHelper.Clamp(_errorSum, _lowerBound, _upperBound); }
			else { _errorSum = _errorSum * (1.0 - _integralDecayRatio) + error * _timeStep; }
			_lastError = error;
			//Construct output
			this.Value = _kP * error + _kI * _errorSum + _kD * errorDerivative;
			return this.Value;
		}
		public double Control(double error, double timeStep) { _timeStep = timeStep; _inverseTimeStep = 1 / _timeStep; return Control(error); }
		public void Reset() { _errorSum = 0; _lastError = 0; _firstRun = true; }
	}
	#endregion
}
//=== End Of Script ===
}
}
