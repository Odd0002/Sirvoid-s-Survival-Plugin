using System;
using System.Collections.Generic;
using System.Threading;
using MCGalaxy.Events;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Network;
using MCGalaxy.Generator;
using MCGalaxy.Blocks;
using MCGalaxy.DB;
using MCGalaxy.Maths;

namespace MCGalaxy
{
	public class survival : Plugin
	{
		public override string name { get { return "survival"; } }
		public override string website { get { return "www.example.com"; } }
		public override string MCGalaxy_Version { get { return "1.9.0.2"; } }
		public override int build { get { return 100; } }
		public override string welcome { get { return "Survival Plugin loaded !"; } }
		public override string creator { get { return "Sirvoid"; } }
		public override bool LoadAtStartup { get { return true; } }
		
		string[, ,] playersInv = new string[50, 35, 3];
		string[,] playersBreakTime = new string[50,2];
		string[,] playersInfo = new string[50,9];
		string[,] mobs = new string[200,11];
		
		List<byte> walkableList = new List<byte>();
		
		static volatile bool ThreadNotDead = true;
		
		Orientation rot;
		
		int vTime = 0;
		int vLast =0;	
		
		string hubMap = "hub";
		
		string lastKillMsg = "";
		string creativePlayer = "";
		
		Thread scheduling;
		Thread mobScheduling;
		Thread mobMoveScheduling;
		
		Thread mobLoadThread;


		
		public override void Load(bool startup)
		{
			Thread.Sleep(100);	
			Chat.MessageAll("%aSurvival plugin loaded ! For more informations , do /help survival"); 
			OnJoinedLevelEvent.Register(HandleOnJoinedLevel, Priority.Low);
			OnBlockChangeEvent.Register(HandleBlockChange, Priority.Low);
			OnPlayerCommandEvent.Register(HandleCommand, Priority.Low);
			OnPlayerDisconnectEvent.Register(HandleOnDisconnected, Priority.Low);
			OnPlayerConnectEvent.Register(HandleOnConnect, Priority.Low);
			OnPlayerClickEvent.Register(HandleClick, Priority.Low);
			
			walkableList.Add(0);
			walkableList.Add(8);
			walkableList.Add(51);
			walkableList.Add(37);
			walkableList.Add(38);
			
			scheduling = new Thread(new ThreadStart(scheduler));
			scheduling.Start();
			
			mobScheduling = new Thread(new ThreadStart(mobScheduler));
			mobScheduling.Start();
			
			mobMoveScheduling = new Thread(new ThreadStart(mobMoveScheduler));
			mobMoveScheduling.Start();
			
			createConnectedList();
			
			Player[] online = PlayerInfo.Online.Items;
			
			foreach (Player p in online){
				if(p.level.name != hubMap){
				RefreshCycleDayNight(p);
				}
				loadInv(p);
			}
			
			foreach (Player p in online){
				for(int i = 0;i<50;i++){
					if(playersInv[i,0,0] == p.name) break;
					if(playersInv[i,0,0] == null){
						playersInv[i,0,0] = p.name;
						break;
					}
				}
				
				for(int i = 0;i<50;i++){
					if(playersBreakTime[i,0] == p.name) break;
					if(playersBreakTime[i,0] == null){
						playersBreakTime[i,0] = p.name;
						break;
					}
				}
				
				for(int i = 0;i<50;i++){
					if(playersInfo[i,0] == p.name) break;
					if(playersInfo[i,0] == null){
						playersInfo[i,0] = p.name;
						playersInfo[i,1] = "20";
						playersInfo[i,2] = "0";
						playersInfo[i,3] = "20";
						playersInfo[i,4] = "0";
						playersInfo[i,5] = "0";
						playersInfo[i,6] = "0";
						playersInfo[i,7] = "";
						break;
					}
				}
			}
			
		}
		                      
		public override void Unload(bool shutdown)
		{

			Player[] online = PlayerInfo.Online.Items;
			foreach (Player pl in online){
				if(pl.level.name == hubMap) continue;
				for(int id = 0;id<150;id++){
				pl.Send(Packet.RemoveEntity((byte)(id+50)));
				}
			}
			
			Chat.MessageAll("%aSurvival plugin unloaded !"); 
			OnBlockChangeEvent.Unregister(HandleBlockChange);
		    OnJoinedLevelEvent.Unregister(HandleOnJoinedLevel);
			OnPlayerCommandEvent.Unregister(HandleCommand);
			OnPlayerDisconnectEvent.Unregister(HandleOnDisconnected);
			OnPlayerConnectEvent.Unregister(HandleOnConnect);
			OnPlayerClickEvent.Unregister(HandleClick);
			ThreadNotDead = false;
			scheduling.Abort();
			mobScheduling.Abort();
			mobMoveScheduling.Abort();
			mobLoadThread.Abort();
			
		}
		
		void setHome(Player p, ushort x, ushort y,ushort z){
			System.IO.FileInfo filedir = new System.IO.FileInfo("./text/survivalPlugin/homes/" + p.name + ".txt");
			filedir.Directory.Create();
		
			System.IO.File.WriteAllText("./text/survivalPlugin/homes/" + p.name + ".txt", p.level.name + "," + x + "," + y + "," + z);
		}
		
		string loadHome(Player p){
			if(!System.IO.File.Exists("./text/survivalPlugin/homes/" + p.name + ".txt")) return "";
			return System.IO.File.ReadAllText("./text/survivalPlugin/homes/" + p.name + ".txt");
		}
		
		string tempInvString;
		void saveInv(Player p){
			
			tempInvString = "0;0,";
			
			for(int i = 0;i<50;i++){
				if(playersInv[i,0,0] == p.name){
					for(int j = 1; j<35; j++){
						if(playersInv[i,j,0] == null || playersInv[i,j,1] == null){
							tempInvString = tempInvString + "0" + ";" + "0" + ";" + "0" + ",";
						}else{
							tempInvString = tempInvString + playersInv[i,j,0] + ";" + playersInv[i,j,1] + ";" + playersInv[i,j,2] + ",";
						}
					}
					break;
				}
			}
			
			System.IO.FileInfo filedir = new System.IO.FileInfo("./text/survivalPlugin/inv/" + p.name + ".txt");
			filedir.Directory.Create();
		
			System.IO.File.WriteAllText("./text/survivalPlugin/inv/" + p.name + ".txt", tempInvString);

		}
		
		string tempInvStringRead;
		void loadInv(Player p){
			
			if(!System.IO.File.Exists("./text/survivalPlugin/inv/" + p.name + ".txt")) return;
			
			tempInvStringRead = System.IO.File.ReadAllText("./text/survivalPlugin/inv/" + p.name + ".txt");
			
			string[] tempslots = tempInvStringRead.Split(',');
			
			
			for(int i = 0;i<50;i++){
				if(playersInv[i,0,0] == null){
					playersInv[i,0,0] = p.name;
					if(tempslots.Length < 35){ playersInv[i,30,0] = "0";playersInv[i,31,0] = "0";playersInv[i,32,0] = "0";playersInv[i,33,0] = "0";}//Debug new inventory format
					for(int j = 1; j<tempslots.Length-1; j++){
						string[] tempSlotData = tempslots[j].Split(';');
						playersInv[i,j,0] = tempSlotData[0];
						playersInv[i,j,1] = tempSlotData[1];
						playersInv[i,j,2] = tempSlotData[2];
						
					}
					break;
				}
			}
			
		}
		
		void removeFromInvs(Player p){
			for(int i = 0;i<50;i++){
				if(playersInv[i,0,0] == p.name){
					playersInv[i,0,0] = null;
					for(int j = 1; j<30; j++){
						playersInv[i,j,0] = null;
						playersInv[i,j,1] = null;
						playersInv[i,j,2] = null;
					}
				}
			}
		}
		
		void refreshInvOrder(Player p, int index){
			int incr = 1;
			for(int i = 1; i < 255;i++){
					if(haveBlock(p,(byte)i,1)){
						p.Send(Packet.SetInventoryOrder((byte)i, (byte)incr++));
					}
					else{
						p.Send(Packet.SetInventoryOrder((byte)i, (byte)255));
					}
			}
			saveInv(p);
		}
		
		void replaceByAir(Player p,byte block){
			
			/*for(int i = 254; i > 246;i--){
				p.Send(Packet.HoldThis((byte)i, false));
			}
			p.Send(Packet.HoldThis(block, false));*/
			
			
		}
		
		void RefreshCycleDayNight(Player pl){
				if(pl.level.name == hubMap) return;
				if(vTime > 0 && vTime < 400000){//day
					pl.SendEnvColor(0,"-1"); //sky
					pl.SendEnvColor(1,"-1"); // cloud
					pl.SendEnvColor(2,"-1"); // fog
					pl.SendEnvColor(3,"-1"); // shadow
					pl.SendEnvColor(4,"-1"); // light
				}
				if((vTime > 400000 && vTime < 630000) || (vTime > 1140000 && vTime < 1200000)){ //sunset
					pl.SendEnvColor(0,"ffd190"); //sky
					pl.SendEnvColor(1,"c1b2b3"); // cloud
					pl.SendEnvColor(2,"c2a296"); // fog
					pl.SendEnvColor(3,"464652"); // shadow
					pl.SendEnvColor(4,"717073"); // light
				}
				if((vTime > 630000 && vTime < 1140000)){ //night
					pl.SendEnvColor(0,"2a2f59"); //sky
					pl.SendEnvColor(1,"070A23"); // cloud
					pl.SendEnvColor(2,"1E223A"); // fog
					pl.SendEnvColor(3,"2f2f3d"); // shadow
					pl.SendEnvColor(4,"515158"); // light
				}
			
		}
		
		void HandleOnJoinedLevel(Player p, Level prevLevel, Level level){
			
			mobLoadThread = new Thread(() => refreshMobs(p)); //And also the day night cycle
			mobLoadThread.Start();
			
			
			
			if(!p.hasCpe) p.Kick("To play here : Launcher -> Option -> Mode -> Enhanced","Option >> Mode >> Enhanced");
			
			Player.Message(p,"%bSurvival Server --►"); 
			Player.Message(p,"%bFor more informations , do %a/help survival"); 
			Player.Message(p,"%bHave fun! %f☺");
			
			if(p.level.name != hubMap){
				RefreshCycleDayNight(p);
			}
			removeFromInvs(p);
		
			loadInv(p);
		
			for(int i = 1; i < 255;i++){
			 p.Send(Packet.SetInventoryOrder((byte)i, (byte)255));
			}
		
			for(int i = 0;i<50;i++){
				if(playersInv[i,0,0] == p.name){
					refreshInvOrder(p,i);
				}
			}
					
			for(int i = 0;i<50;i++){
				if(playersBreakTime[i,0] == p.name) break;
				if(playersBreakTime[i,0] == null){
					playersBreakTime[i,0] = p.name;
					break;
				}
			}
			
			for(int i = 0;i<50;i++){
				if(playersInfo[i,0] == p.name) break;
				if(playersInfo[i,0] == null){
					playersInfo[i,0] = p.name;
					playersInfo[i,1] = "20";
					playersInfo[i,2] = "0";
					playersInfo[i,3] = "20";
					playersInfo[i,4] = "0";
					playersInfo[i,5] = "0";
					playersInfo[i,6] = "0";
					playersInfo[i,7] = "";
					break;
				}
			}
			
			for(int i = 0;i<50;i++){
				if(playersInv[i,0,0] == p.name) return;
			}
			
			for(int i = 0;i<50;i++){
				if(playersInv[i,0,0] == null){
					playersInv[i,0,0] = p.name;
					break;
				}
			}
			

			
				
			//refreshArmors(p);
		
			
		}
		
		void HandleOnConnect(Player p){
			 createConnectedList();
			 p.Send(Packet.HoldThis((byte)254, false));
			 p.Send(Packet.HoldThis((byte)4, false));
			 p.Send(Packet.HoldThis((byte)253, false));
			 p.Send(Packet.HoldThis((byte)45, false));
			 p.Send(Packet.HoldThis((byte)252, false));
			 p.Send(Packet.HoldThis((byte)3, false));
			 p.Send(Packet.HoldThis((byte)251, false));
			 p.Send(Packet.HoldThis((byte)5, false));
			 p.Send(Packet.HoldThis((byte)250, false));
			 p.Send(Packet.HoldThis((byte)17, false));
			 p.Send(Packet.HoldThis((byte)249, false));
			 p.Send(Packet.HoldThis((byte)18, false));
			 p.Send(Packet.HoldThis((byte)248, false));
			 p.Send(Packet.HoldThis((byte)2, false));
			 p.Send(Packet.HoldThis((byte)247, false));
			 p.Send(Packet.HoldThis((byte)44, false));
			 p.Send(Packet.HoldThis((byte)246, false));
		}
		
		void createConnectedList(){
			string tempPstring = "";
			
			Player[] online = PlayerInfo.Online.Items;
				foreach (Player pl in online){
					for(int i = 0;i<50;i++){
						if(playersInv[i,0,0] == pl.name){
							tempPstring += pl.truename + ";" + playersInv[i,30,0] + ";" + playersInv[i,31,0] + ";" + playersInv[i,32,0] + ";" + playersInv[i,33,0] + ",";
						}
					}
			}
			System.IO.FileInfo filedir = new System.IO.FileInfo("./plugins/armors/connected.txt");
			filedir.Directory.Create();
		
			
			System.IO.File.WriteAllText("./plugins/armors/connected.txt", tempPstring);

		}
		
		void HandleOnDisconnected(Player p, string reason){
			removeFromInvs(p);
			 createConnectedList();
			for(int i = 0;i<50;i++){
				if(playersBreakTime[i,0] == p.name){
					playersBreakTime[i,0] = null;
					playersBreakTime[i,1] = null;
				}
				if(playersInfo[i,0] == p.name){
					playersInfo[i,0] = null;
					playersInfo[i,1] = null;
					playersInfo[i,2] = null;
					playersInfo[i,3] = null;
					playersInfo[i,4] = null;
					playersInfo[i,5] = null;
					playersInfo[i,6] = null;
					playersInfo[i,7] = null;
				}
			}
		}
		
		bool isInventoryFull(int index){
			int iCount = 0;
			for(int i = 1; i<30; i++){
				if(playersInv[index,i,0] != "0" && playersInv[index,i,0] != null){
					iCount++;
				}
			}
			return iCount >= 29;
		}
		
		void addToInv(int index,byte block,int quantity,int durability){
			if(block >= 246 && block <= 254) return;
			for(int i = 1; i<30; i++){
				if(playersInv[index,i,0] == "" + block){
					playersInv[index,i,1] = (int.Parse(playersInv[index,i,1]) + quantity) + "";
					return;
				}
				
			}
			for(int i = 1; i<30; i++){
				if(playersInv[index,i,0] == null || playersInv[index,i,0] == "0"){
						playersInv[index,i,0] = "" + block;
						playersInv[index,i,1] = quantity + "";
						playersInv[index,i,2] = durability + "";
						return;
				}
			}
			Player[] online = PlayerInfo.Online.Items;
			foreach (Player p in online){
				if(p.name == playersInv[index,0,0]) Player.Message(p,"%cYour inventory is full !");
			}
		}
		
		void subToInv(int index,Player p,byte block,int quantity){
			for(int i = 1; i<30; i++){
				if(playersInv[index,i,0] == "" + block){
					playersInv[index,i,1] = (int.Parse(playersInv[index,i,1]) - quantity) + "";
					if(int.Parse(playersInv[index,i,1]) < 1){
						playersInv[index,i,0] = "0";
						playersInv[index,i,1] = "0";
						playersInv[index,i,2] = "0";
						replaceByAir(p,block);
					}
					return;
				}
			}
		}
		 
		void subToDurability(int index,byte block,Player p){
			for(int i = 1; i<30; i++){
				if(playersInv[index,i,0] == "" + block){
					playersInv[index,i,2] = (int.Parse(playersInv[index,i,2])-1) + "";
					if(int.Parse(playersInv[index,i,2]) <= 0) {
						playersInv[index,i,2] = getDurability(block) + "";
						playersInv[index,i,1] = (int.Parse(playersInv[index,i,1]) - 1) + "";
					}
					if(int.Parse(playersInv[index,i,1]) < 1){
						playersInv[index,i,0] = "0";
						playersInv[index,i,1] = "0";
						playersInv[index,i,2] = "0";
					}
					return;
				}
			}
		}
		
		int counting;
		bool haveBlock(Player p,byte block,int quantity){
			
			int index = 0;
			
			for(int i = 0;i<50;i++){
				if(playersInv[i,0,0] == p.name){
					index = i;
				}
			}
		
			counting = 0;
			for(int i = 1; i<30; i++){
				if(playersInv[index,i,0] == "" + block) {
					if(int.Parse(playersInv[index,i,1]) >= quantity){
						return true;
					} else {
						counting += int.Parse(playersInv[index,i,1]);
					}
				}
				if(counting >= quantity){return true;}
			}

			return false;
		}
		
		string getBlockQuantity(Player p, byte block){
			for(int i = 0;i<50;i++){
				if(playersInv[i,0,0] == p.name){
					for(int j = 1; j<30; j++){
						if(playersInv[i,j,0] == "" + block){
							return playersInv[i,j,1];
						}
					}
				}
			}
			return "0";
		}
		
		string getBlockDurability(Player p, byte block){
			for(int i = 0;i<50;i++){
				if(playersInv[i,0,0] == p.name){
					for(int j = 1; j<30; j++){
						if(playersInv[i,j,0] == "" + block){
							return playersInv[i,j,2];
						}
					}
				}
			}
			return "0";
		}
		
		void openDoor(Player p,ushort x,ushort y,ushort z){
			byte tiletoset = 0;
			
			if(p.level.GetExtTile(x,y,z) == 78){
				p.level.SetExtTile(x,y,z,80);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(80));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(80));
				
				p.level.SetExtTile(x,++y,z,84);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(84));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(84));
			}
			else if(p.level.GetExtTile(x,y,z) == 79){
				p.level.SetExtTile(x,y,z,81);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(81));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(81));
				
				p.level.SetExtTile(x,++y,z,85);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(85));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(85));
			}
			else if(p.level.GetExtTile(x,y,z) == 80){
				p.level.SetExtTile(x,y,z,78);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(78));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(78));
				
				p.level.SetExtTile(x,++y,z,82);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(82));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(82));
			}
			else if(p.level.GetExtTile(x,y,z) == 81){
				p.level.SetExtTile(x,y,z,79);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(79));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(79));
				
				p.level.SetExtTile(x,++y,z,83);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(83));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(83));
			}
			else if(p.level.GetExtTile(x,y,z) == 82){
				p.level.SetExtTile(x,y,z,84);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(84));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(84));
				
				p.level.SetExtTile(x,--y,z,80);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(80));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(80));
			}
			else if(p.level.GetExtTile(x,y,z) == 83){
				p.level.SetExtTile(x,y,z,85);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(85));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(85));
				
				p.level.SetExtTile(x,--y,z,81);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(81));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(81));
			}
			else if(p.level.GetExtTile(x,y,z) == 84){
				p.level.SetExtTile(x,y,z,82);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(82));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(82));
				
				p.level.SetExtTile(x,--y,z,78);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(78));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(78));
			}
			else if(p.level.GetExtTile(x,y,z) == 85){
				p.level.SetExtTile(x,y,z,83);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(83));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(83));
				
				p.level.SetExtTile(x,--y,z,79);
				p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(79));
				Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(79));
			}
			
		}
		
		void setHpIndicator(int i,Player pl){
			int a = playersInfo[i,1] != null ? int.Parse(playersInfo[i,1]) : 20;

			string hpstring = "";
			for (int h = 0;h <= 20;h++){
				if(h%2 != 0){
					if(h >= 20-a){
						if(h == 20-a){
							hpstring = hpstring + "%4♥";
						}else{		
							hpstring = hpstring + "%c♥";
						}
					}else{								
							hpstring = hpstring + "%0♥";
					}
				}	
			}
			pl.SendCpeMessage(CpeMessageType.Status1, "%4"+hpstring);
			
			
			int a2= playersInfo[i,3] != null ? int.Parse(playersInfo[i,3]) : 20;
			string breathstring = "";
			for (int h = 0;h <= 20;h++){
				if(h%2 != 0){
					if(h >= 20-a2){
						if(h == 20-a2){
							breathstring = breathstring + "%3○";
						}else{		
							breathstring = breathstring + "%b○";
						}
					}else{								
							breathstring = breathstring + "%0○";
					}
				}	
			}
			pl.SendCpeMessage(CpeMessageType.Status2, "%3"+breathstring);
			
		}
		
		int getToolDamage(byte b){
			switch(b){
				case 86:
					return 4;
				case 87:
					return 5;
				case 98:
					return 6;
				case 102:
					return 7;
				default:
					return 1;
			}
		}
		
		void HandleClick(Player p, MouseButton button, MouseAction action,
					     ushort yaw, ushort pitch, byte entity,
					     ushort x, ushort y, ushort z, TargetBlockFace face){		
			if (button == MouseButton.Left){
				if(action == MouseAction.Pressed){
					int curpid = 0;
					for(int yi = 0;yi<50;yi++){					
						if(playersInfo[yi,0] == p.name){
							curpid = yi;
						}
					}
					byte heldID = p.GetHeldBlock().ExtID;
					int pDamage = haveBlock(p,heldID,1) ? getToolDamage(heldID) : 1;
					
					if(int.Parse(DateTime.Now.Second + "" + DateTime.Now.Millisecond) - int.Parse(playersInfo[curpid,2])>300 || int.Parse(DateTime.Now.Second+""+DateTime.Now.Millisecond) - int.Parse(playersInfo[curpid,2])<-300){
						Player[] online = PlayerInfo.Online.Items;
						foreach (Player pl in online){ //PvP
							if (pl.EntityID == entity){ 
								for(int i = 0;i<50;i++){
									if(playersInfo[i,0] == pl.name){
										double dist = (Math.Sqrt(Math.Pow(Math.Abs(p.Pos.X - pl.Pos.X), 2) + Math.Pow(Math.Abs(p.Pos.Y - pl.Pos.Y), 2) + Math.Pow(Math.Abs(p.Pos.Z - pl.Pos.Z), 2)));
										if (dist < 150){
											if(isTool(heldID)) subToDurability(curpid,heldID,p);
											refreshInvOrder(p,curpid);
											//int pDamagePlayer = pDamage * ( 1 - Math.Min(20, Math.Max( getPlayerDef(i) / 5,  getPlayerDef(i)- pDamage / 2 ) ) / 25 );
											playersInfo[i,1] = (int.Parse(playersInfo[i,1]) - pDamage) + "";
											setHpIndicator(i,pl);
											
											Position new_pos;
											new_pos.X = pl.Pos.X +(int)((pl.Pos.X-p.Pos.X)/2);
											new_pos.Y = pl.Pos.Y;
											new_pos.Z = pl.Pos.Z +(int)((pl.Pos.Z-p.Pos.Z)/2);
											
											Position new_midpos;
											new_midpos.X = pl.Pos.X +(int)((pl.Pos.X-p.Pos.X)/4);
											new_midpos.Y = pl.Pos.Y;
											new_midpos.Z = pl.Pos.Z +(int)((pl.Pos.Z-p.Pos.Z)/4);
											
											if (pl.level.IsAirAt((ushort)new_pos.BlockX, (ushort)new_pos.BlockY, (ushort)new_pos.BlockZ) && pl.level.IsAirAt((ushort)new_pos.BlockX, (ushort)(new_pos.BlockY-1), (ushort)new_pos.BlockZ) && 
											pl.level.IsAirAt((ushort)new_midpos.BlockX, (ushort)new_midpos.BlockY, (ushort)new_midpos.BlockZ) && pl.level.IsAirAt((ushort)new_midpos.BlockX, (ushort)(new_midpos.BlockY-1), (ushort)new_midpos.BlockZ)){
												if(pl.Pos.BlockX != p.level.SpawnPos.BlockX && pl.Pos.BlockY != p.level.SpawnPos.BlockY && pl.Pos.BlockY != p.level.SpawnPos.BlockZ){pl.SendPos(Entities.SelfID, new Position(new_pos.X, new_pos.Y, new_pos.Z), pl.Rot);}
											}
											
											
											if(int.Parse(playersInfo[i,1]) < 1){
												playersInfo[i,1] = "20";
												pl.SendPos(Entities.SelfID, new Position(pl.Level.SpawnPos.X, pl.Level.SpawnPos.Y, pl.Level.SpawnPos.Z), pl.Rot);
												if(lastKillMsg != pl.ColoredName + " %Swas slain by " +  p.ColoredName + "."){
													Chat.MessageLevel(pl.level, pl.ColoredName + " %Swas slain by " +  p.ColoredName + ".");
												}
												lastKillMsg = pl.ColoredName + " %Swas slain by " +  p.ColoredName + ".";
												pl.SendCpeMessage(CpeMessageType.Status1, "%c♥♥♥♥♥♥♥♥♥♥");
											}
										}
									}
								}
							}
						}
						playersInfo[curpid,2] =  DateTime.Now.Second + "" + DateTime.Now.Millisecond + "";
					}
					
					if(entity > 49 && entity < 255){
						byte mobID = (byte)(entity-50);
						if(mobs[mobID,0] != null) {
							double dist = (Math.Sqrt(Math.Pow(Math.Abs(p.Pos.X - int.Parse(mobs[mobID,1])), 2) + Math.Pow(Math.Abs(p.Pos.Y - int.Parse(mobs[mobID,2])), 2) + Math.Pow(Math.Abs(p.Pos.Z - int.Parse(mobs[mobID,3])), 2)));
							if (dist < 150){
								if(isTool(heldID)) subToDurability(curpid,heldID,p);
								refreshInvOrder(p,curpid);
								mobs[mobID,4] = (int.Parse(mobs[mobID,4])-pDamage) + "";
								mobs[mobID,8] = "1";
								
								
								
								int mobX = int.Parse(mobs[mobID,1]);
								int mobY = int.Parse(mobs[mobID,2]);
								int mobZ = int.Parse(mobs[mobID,3]);
								
								Position new_pos;
								new_pos.X = mobX +(int)((mobX-p.Pos.X)/2);
								new_pos.Y = mobY;
								new_pos.Z = mobZ +(int)((mobZ-p.Pos.Z)/2);
								
								Position new_midpos;
								new_midpos.X = mobX +(int)((mobX-p.Pos.X)/4);
								new_midpos.Y = mobY;
								new_midpos.Z = mobZ +(int)((mobZ-p.Pos.Z)/4);

								if (p.level.IsAirAt((ushort)new_pos.BlockX, (ushort)new_pos.BlockY, (ushort)new_pos.BlockZ) && p.level.IsAirAt((ushort)new_pos.BlockX, (ushort)(new_pos.BlockY-1), (ushort)new_pos.BlockZ) && 
								p.level.IsAirAt((ushort)new_midpos.BlockX, (ushort)new_midpos.BlockY, (ushort)new_midpos.BlockZ) && p.level.IsAirAt((ushort)new_midpos.BlockX, (ushort)(new_midpos.BlockY-1), (ushort)new_midpos.BlockZ)){
									if(p.Pos.BlockX != p.level.SpawnPos.BlockX && p.Pos.BlockY != p.level.SpawnPos.BlockY && p.Pos.BlockY != p.level.SpawnPos.BlockZ){
										
										mobs[mobID,1] = new_pos.X + "";
										mobs[mobID,2] = new_pos.Y + "";
										mobs[mobID,3] = new_pos.Z + "";
										
										Player[] online = PlayerInfo.Online.Items;
										foreach (Player pl in online){
											pl.Send(Packet.Teleport((byte)(mobID+50), new_pos, rot, true));
										}
									}
								}
								
								
								
								if(int.Parse(mobs[mobID,4]) < 1){
									
									
									if(mobs[mobID,0] == "pig"){
										ushort x2 = (ushort)(int.Parse(mobs[mobID,1])/32);
										ushort y2 = (ushort)((int.Parse(mobs[mobID,2])-1)/32);
										ushort z2 = (ushort)(int.Parse(mobs[mobID,3])/32);
										
										if(p.level.IsAirAt(x2, y2, z2)){
										p.level.SetExtTile(x2,y2,z2,89);
										p.level.UpdateBlock(p, x2, y2, z2, ExtBlock.FromRaw(89));
										Player.GlobalBlockchange(p.level, x2, y2, z2, ExtBlock.FromRaw(89));
										}									
									}
									
									if(mobs[mobID,0] == "sheep"){
										ushort x2 = (ushort)(int.Parse(mobs[mobID,1])/32);
										ushort y2 = (ushort)((int.Parse(mobs[mobID,2])-1)/32);
										ushort z2 = (ushort)(int.Parse(mobs[mobID,3])/32);
										
										if(p.level.IsAirAt(x2, y2, z2)){
										p.level.SetExtTile(x2,y2,z2,36);
										p.level.UpdateBlock(p, x2, y2, z2, ExtBlock.FromRaw(36));
										Player.GlobalBlockchange(p.level, x2, y2, z2, ExtBlock.FromRaw(36));
										}									
									}
									
									removeMob(mobID,p.level.name);
								}
							}
						}
					}
					for(int i = 0;i<50;i++){ //Block Breaking
						if(p.Level.name == "survival") return;
						if(p.Level.name == hubMap) return;
						if(playersBreakTime[i,0] == p.name){
							if(!isInventoryFull(i)){
								if(playersBreakTime[i,1] == null) playersBreakTime[i,1] = "0";
								playersBreakTime[i,1] = (int.Parse(playersBreakTime[i,1]) + 1) + "";	
								int tempBlockBP = (int)((float.Parse(playersBreakTime[i,1])/getBlockHp(p,p.level.GetBlock(x,y,z)))*100);
								int blockBP = tempBlockBP > 100 || tempBlockBP < 0 ? 100 : tempBlockBP;
								p.SendCpeMessage(CpeMessageType.Status3, "Breaking Block: " +  blockBP + "%");
								if(int.Parse(playersBreakTime[i,1]) >= getBlockHp(p,p.level.GetBlock(x,y,z))){
								p.ManualChange(x, y, z, false, ExtBlock.Air, false);
								playersBreakTime[i,1] = "0";
								}
							}
						}
					}
					return;
				}
				else if (action == MouseAction.Released){
					for(int i = 0;i<50;i++){
						if(playersBreakTime[i,0] == p.name){
							playersBreakTime[i,1] = "0";
							p.SendCpeMessage(CpeMessageType.Status3, "");
							
						}
					}
				}
			}
			if (button == MouseButton.Right){
				if(action == MouseAction.Pressed){
					
					if(p.level.GetExtTile(x,y,z) <= 86 && p.level.GetExtTile(x,y,z) >= 78) openDoor(p,x,y,z);
					if(p.level.GetExtTile(x,y,z) >= 111 && p.level.GetExtTile(x,y,z) <= 114) accessChest(p,x,y,z);
					
					if(p.level.GetExtTile(x,y,z) >= 103 && p.level.GetExtTile(x,y,z) <= 110){
						if(vTime > 630000 && vTime < 1140000){
							setHome(p,x,y,z);
							Player.Message(p,"%eHome set!");
						}else{
							Player.Message(p,"%eYou can only set your home at night!");
						}
					}
					
					for(int i = 0;i<50;i++){
						if(playersInv[i,0,0]  == p.name){ 
							byte heldBlock = p.GetHeldBlock().ExtID;
							++y;						
							if(heldBlock == 92){
								if(p.Level.name == hubMap) return;
								if(p.level.GetBlock(x,y,z).BlockID == 8 || p.level.GetBlock(x,y,z).BlockID == 9 && haveBlock(p,92,1)) {
									subToInv(i,p,92,1);
									addToInv(i,93,1,0);
									Player.Message(p,"%eYou filled your bucket!");
									p.level.SetExtTile(x,y,z,0);
									p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(0));
									Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(0));
									refreshInvOrder(p, i);
								}
							}
							if(heldBlock == 93){
								if(p.Level.name == hubMap) return;
								if(p.level.GetBlock(x,y,z).BlockID != 8 && p.level.GetBlock(x,y,z).BlockID != 9 && haveBlock(p,93,1)) {
									subToInv(i,p,93,1);
									addToInv(i,92,1,0);
									Player.Message(p,"%eYou have empty your bucket!");
									if(p.level.IsAirAt(x, y, z)){
										p.level.SetExtTile(x,y,z,9);
										p.level.UpdateBlock(p, x, y, z, ExtBlock.FromRaw(9));
										Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.FromRaw(9));
									}	
									refreshInvOrder(p, i);
								}
							} 
							--y;
						}
					
						if(playersInfo[i,0] == p.name){ 
							
							byte heldBlock = p.GetHeldBlock().ExtID;
							byte food = 0;
							int hpregen = 0;
							
							if(haveBlock(p,89,1) && heldBlock == 89){ food = 89; hpregen = 3; }
							if(haveBlock(p,90,1) && heldBlock == 90){ food = 90; hpregen = 6; }
							
							if(int.Parse(playersInfo[i,1]) > 19 || food == 0) return;
							playersInfo[i,1] = (int.Parse(playersInfo[i,1]) + hpregen) + "";
							
							subToInv(i,p,food,1);
							refreshInvOrder(p, i);
							
							if(int.Parse(playersInfo[i,1]) > 19) playersInfo[i,1] = "20";
							setHpIndicator(i,p);
							return;
							
						}

					}
				}
			}
		}
		
		void chestPlaceItem(Player p,byte item, int quantity, int durability){
				
				
				int infoIndex = 0;
				int invIndex = 0;
				string chestDataStr = "";
				string[,] chestData = new string[28,3];
				
				for(int i = 0;i<50;i++){
					if(playersInfo[i,0] == p.name){
						infoIndex = i;
					}
				}
				
				
				for(int i = 0;i<50;i++){
					if(playersInv[i,0,0] == p.name){
						invIndex = i;
					}
				}
				
				if(!haveBlock(p,item,quantity)) return;
				subToInv(invIndex,p,item,quantity);
				
				string[] chestInfo = playersInfo[infoIndex,0].Split(',');
				string chestDir = "./text/survivalPlugin/chests/" + chestInfo[0] + "/" + chestInfo[1] + "," + chestInfo[2] + "," + chestInfo[3] + ".txt";
				
				//Loading
				if(!System.IO.File.Exists(chestDir)){
					for(int i = 0;i<27;i++){
						chestDataStr += "0;0;0,";
					}
				}else{
					chestDataStr = System.IO.File.ReadAllText(chestDir);
				}
					
				string[] tempChestData = chestDataStr.Split(',');
				for(int i = 0;i<27;i++){
					string[] tempChestData2 = tempChestData[i].Split(';');
					chestData[i,0] = tempChestData2[0];
					chestData[i,1] = tempChestData2[1];
					chestData[i,2] = tempChestData2[2];
				}
				//
				
				//Adding
				for(int i = 1; i<27; i++){
					if(chestData[i,0] == "" + item){
						chestData[i,1] = (int.Parse(chestData[i,1]) + quantity) + "";
					}
					
				}
				for(int i = 1; i<30; i++){
					if(chestData[i,0] == null || chestData[i,0] == "0"){
							chestData[i,0] = "" + item;
							chestData[i,1] = quantity + "";
							chestData[i,2] = durability + "";
					}
				}	
				
				//Saving
				chestDataStr = "";
				for(int i = 0;i<27;i++){
					chestDataStr += chestData[i,0] + ";" + chestData[i,1] + ";" + chestData[i,2] + ",";
				}
				
				System.IO.FileInfo filedir = new System.IO.FileInfo(chestDir);
				filedir.Directory.Create();
		
				System.IO.File.WriteAllText(chestDir, chestDataStr);
		}
		
		void accessChest(Player p,ushort x,ushort y,ushort z){
			for(int i = 0;i<50;i++){
				if(playersInfo[i,0] == p.name){
					playersInfo[i,7] =  p.level.name + "," + x + "," + y + "," + z;
					Player.Message(p,"%eAccessing the chest at " + x + " " + y + " " + z + ".");
				}
			}
		}
		
		int getToolSpeed(ExtBlock tool,int type){
			if(type == 0){ //pickaxe
				switch(tool.ExtID){
					case 66:
						return 2;
					case 70:
						return 5;
					case 73:
						return 8;
					case 99:
						return 12;
					default:
						return 1;
				}
			} else if(type == 1){ //axe
				switch(tool.ExtID){
					case 68:
						return 2;
					case 71:
						return 4;
					case 74:
						return 7;
					case 100:
						return 10;
					default:
						return 1;
				}
			} else if(type == 2){ //shovel
				switch(tool.ExtID){
					case 69:
						return 2;
					case 72:
						return 4;
					case 75:
						return 7;
					case 101:
						return 10;
					default:
						return 1;
				}
			}
			
			return 1;
		}
				
		float getBlockHp(Player p ,ExtBlock block){
			
			
			int pickSpeed = haveBlock(p,p.GetHeldBlock().ExtID,1) ? getToolSpeed(p.GetHeldBlock(),0) : 1;
			int axeSpeed = haveBlock(p,p.GetHeldBlock().ExtID,1)  ? getToolSpeed(p.GetHeldBlock(),1) : 1;
			int shovelSpeed = haveBlock(p,p.GetHeldBlock().ExtID,1) ? getToolSpeed(p.GetHeldBlock(),2) : 1;
			
			if(isTool(p.GetHeldBlock().ExtID) && !haveBlock(p,p.GetHeldBlock().ExtID,1)){
				Player.Message(p,"%cYou do not have this tool.");
			}
			
			switch (p.level.BlockName(block))
			{
				case "Stone":
				case "Cobblestone":
				case "MossyRocks":
				case "Brick":
					return 30/pickSpeed;
				case "Slab":
					return 30/pickSpeed;
				case "DoubleSlab":
					return 30/pickSpeed;
				case "Grass":
					return 3/shovelSpeed;
				case "Dirt":
					return 3/shovelSpeed;
				case "Sand":
					return 3/shovelSpeed;
				case "Gravel":
					return 3/shovelSpeed;
				case "Coal":
					return 60/pickSpeed;
				case "Log":
				case "Wood":
				case "Crate":
				case "BookShelf":
					return 12/axeSpeed;
				case "Iron_Ore":
					return 60/pickSpeed;
				case "Iron":
					return 60/pickSpeed;
				case "Gold_Ore":
					return 60/pickSpeed;
				case "Gold":
					return 60/pickSpeed;
				case "Redstone_Ore":
					return 60/pickSpeed;
				case "Diamond_Ore":
				case "Diamond":
					return 80/pickSpeed;
				case "DoorBase-S":
				case "DoorBase-N":
				case "DoorBase-W":
				case "DoorBase-E":
				case "DoorTop-S":
				case "DoorTop-N":
				case "DoorTop-E":
				case "DoorTop-W":
					return 12/axeSpeed;
				case "BedBase-N":
				case "BedBase-S":
				case "BedBase-W":
				case "BedBase-E":
				case "BedTop-N":
				case "BedTop-S":
				case "BedTop-W":
				case "BedTop-E":
					return 7/axeSpeed;
				case "Rope":
					return 3;
				case "Glass":
					return 3;
				case "White":
					return 4;
				default:
					return 1;
			}
			

		}
		
		int getPlayerDef(int index){
			int def = 
				getArmorDef(int.Parse(playersInv[index,30,0])) +
				getArmorDef(int.Parse(playersInv[index,31,0])) +
				getArmorDef(int.Parse(playersInv[index,32,0])) +
				getArmorDef(int.Parse(playersInv[index,33,0]));
			return def;
		}
		
		int getArmorDef(int b){
			switch(b){
				case 94:
					return 2;
				case 95:
					return 6;
				case 96:
					return 5;
				case 97:
					return 2;
				default:
					return 0;
			}
		}
		
		byte blockCollected(byte b){
			switch(b){
			case 1: //Stone -> Cobblestone
				return 4;
			case 2: //Grass -> Dirt
				return 3;
			case 15://Iron_Ore -> Iron_Block
				return 42;
			case 14://Gold_Ore -> Gold_Block
				return 41;
			case 78:
			case 79:
			case 80:
			case 81:
			case 82:
			case 83:
			case 84:
			case 85:
				return 78;
			case 103:
			case 104:
			case 105:
			case 106:
			case 107:
			case 108:
			case 109:
			case 110:
				return 103;
			case 111:
			case 112:
			case 113:
			case 114:
				return 111;
			case 76:
				return 91;
			case 64:
				return 5;
			default: //X -> X
				return b;
			}
			return b;
		}
		
		byte quantityCollected(byte b){
			switch(b){
			case 64:
				return 20;
			default: //X -> X
				return 1;
			}
			return 1;
		}
		
		bool isTool(byte b){
			switch(b){
				case 66:
				case 68:
				case 69:
				case 70:
				case 71:
				case 72:
				case 73:
				case 74:
				case 75:
				case 86:
				case 87:
				case 98:
				case 99:
				case 100:
				case 101:
				case 102:
					return true;
				default:
					return false;
			}
		}
		
		int getDurability(byte b){
			switch(b){
				case 66:
				case 68:
				case 69:
				case 86:
					return 33;
				case 70:
				case 71:
				case 72:
				case 87:
					return 65;
				case 73:
				case 74:
				case 75:
				case 98:
					return 129;
				case 99:
				case 100:
				case 101:
				case 102:
					return 257;
				default:
					return 0;
			}
		}
		
		void HandleBlockChange(Player p, ushort x, ushort y, ushort z, ExtBlock block, bool placing){
							
				byte block_ID = (block.BlockID == 163 ? block.ExtID : block.BlockID);
				if(!placing){

						ExtBlock b = p.level.GetBlock(x,y,z);
						byte b_ID = (b.BlockID == 163 ? b.ExtID : b.BlockID);
						if(p.level.BlockName(b) != "Air" && p.level.BlockName(b) != "air" && p.level.BlockName(b) != "Bedrock"){
							//Chat.MessageAll("" + b.BlockID);
							for(int i = 0;i<50;i++){
								if(playersInv[i,0,0] == p.name){
									if(isTool(p.GetHeldBlock().ExtID)){subToDurability(i,p.GetHeldBlock().ExtID,p);}
									addToInv(i,blockCollected(b_ID),quantityCollected(b_ID),0);
									refreshInvOrder(p,i);
								}
							}
							
							if(b_ID >= 78 && b_ID < 82) {
								p.level.SetExtTile(x,++y,z,0);
								p.level.UpdateBlock(p, x, y, z, ExtBlock.Air);
								Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.Air);
							} else if(b_ID >= 82 && b_ID < 86){
								p.level.SetExtTile(x,--y,z,0);
								p.level.UpdateBlock(p, x, y, z, ExtBlock.Air);
								Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.Air);
							}
							
							if(b_ID == 107 || b_ID == 104) {
								if(!(p.level.GetExtTile(x,y,++z) >= 103 && p.level.GetExtTile(x,y,z) <= 110)) return;
								p.level.SetExtTile(x,y,z,0);
								p.level.UpdateBlock(p, x, y, z, ExtBlock.Air);
								Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.Air);
							
							} else if(b_ID == 103 || b_ID == 108) {
								if(!(p.level.GetExtTile(x,y,--z) >= 103 && p.level.GetExtTile(x,y,z) <= 110)) return;
								p.level.SetExtTile(x,y,z,0);
								p.level.UpdateBlock(p, x, y, z, ExtBlock.Air);
								Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.Air);
							
							} else if(b_ID == 110 || b_ID == 105) {
								if(!(p.level.GetExtTile(--x,y,z) >= 103 && p.level.GetExtTile(x,y,z) <= 110)) return;
								p.level.SetExtTile(x,y,z,0);
								p.level.UpdateBlock(p, x, y, z, ExtBlock.Air);
								Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.Air);
							
							} else if(b_ID == 109 || b_ID == 106) {
								if(!(p.level.GetExtTile(++x,y,z) >= 103 && p.level.GetExtTile(x,y,z) <= 110)) return;
								p.level.SetExtTile(x,y,z,0);
								p.level.UpdateBlock(p, x, y, z, ExtBlock.Air);
								Player.GlobalBlockchange(p.level, x, y, z, ExtBlock.Air);
							
							}
						}
				} else {
					for(int i = 0;i<50;i++){
						if(playersInv[i,0,0] == p.name){
							if(creativePlayer == p.name) return;
							if(block_ID >= 111 && block_ID <= 114) block_ID = 111;
							if((haveBlock(p,block_ID,1) && p.level.BlockName(block) != "Air" && p.level.BlockName(block) != "air" && block_ID != 103)){
								if((!isTool(block_ID)) && block_ID != 92 && block_ID != 93) {subToInv(i,p,block_ID,1);}
								refreshInvOrder(p,i);
								p.cancelBlock = false;
							}
							else{
								if(block_ID >= 78 && block_ID <= 81 && haveBlock(p,78,1)) { subToInv(i,p,78,1);return;}
								if(block_ID >= 103 && block_ID <= 106 && haveBlock(p,103,1)) { 
									if(block_ID >= 103 && block_ID <= 106){
										int bedX = 0;
										int bedZ = 0;
										byte bedBlock = 0;

										if(block_ID == 103){ bedX = 0; bedZ = -1; bedBlock = 107;}
										if(block_ID == 104){ bedX = 0; bedZ = 1; bedBlock = 108;}
										if(block_ID == 105){ bedX = -1; bedZ = 0; bedBlock = 109;}
										if(block_ID == 106){ bedX = 1; bedZ = 0; bedBlock = 110;}

										
										
										if(((int)x + bedX) >= 0) x = (ushort)((int)x + bedX);
										if(((int)z + bedZ) >= 0) z = (ushort)((int)z + bedZ);
																	
										ExtBlock bBlock = ExtBlock.FromRaw(bedBlock);
										if(p.level.IsAirAt(x,y,z)){
											p.level.SetExtTile(x,y,z,bedBlock);
											p.level.UpdateBlock(p, x, y, z, bBlock);
											Player.GlobalBlockchange(p.level, x, y, z, bBlock);
										}
									}
									subToInv(i,p,103,1);
									return;
								}
								if(p.level.BlockName(block) != "Air" && p.level.BlockName(block) != "air"){
									Player.Message(p, "%cYou are out of " + p.level.BlockName(block) + " !");
								}
								p.cancelBlock = true;
								p.RevertBlock(x,y,z);
							}
							if(isTool(block_ID) || block_ID == 92 || block_ID == 93) {
								p.cancelBlock = true;
								p.RevertBlock(x,y,z);
							}	
						}
					}
				}
			
		}
			
		int countMobs(){
			int mCount = 0;
			for(int i = 0;i<150;i++){
				if(mobs[i,0] != null) {
					mCount++;
				}
			}
			return mCount;
		}
		
		int countMobsModel(string model){
			int mCount = 0;
			for(int i = 0;i<150;i++){
				if(mobs[i,0] == model) {
					mCount++;
				}
			}
			return mCount;
		}
		
		int spawningTimeCounter = 0;
		int spawningMobincrLvl = 0;
		void spawningMob(){
			spawningTimeCounter++;
			if(spawningTimeCounter>5){
				if(countMobs() < 150){
					Random rnd2 = new Random();
					int mRand2 = rnd2.Next(0,2);
					Level lvl;
					
					if(spawningMobincrLvl > 1) spawningMobincrLvl = 0;
					
					if(spawningMobincrLvl == 0){
						lvl = LevelInfo.FindExact("survival");
					} else {
						lvl = LevelInfo.FindExact("newsurvival");
					}
					
					spawningMobincrLvl++;
					
					if(lvl != null){
						Random rnd = new Random();
						int mRand = rnd.Next(0,3);
						
						ushort x = (ushort)rnd.Next(10, lvl.Width-10);
						ushort z = (ushort)rnd.Next(10, lvl.Length-10); 
					
						if(mRand == 0 && vTime > 630000 && vTime < 1140000 && countMobsModel("zombie") < 50){
							for(ushort y = 10;y<110;y++){
								ushort nx = (ushort)(x*32);
								ushort ny = (ushort)(y*32);
								ushort nz = (ushort)(z*32);
								if(lvl.GetTile(x,y,z) == 0){
									addMob(lvl.name,"zombie",nx,(ushort)((int)ny+64),nz,20);
									break;
								}
							}
						}
						if(mRand == 1 && vTime < 630000 && countMobsModel("pig") < 50){
							for(ushort y = 110;y>10;y--){
								ushort nx = (ushort)(x*32);
								ushort ny = (ushort)(y*32);
								ushort nz = (ushort)(z*32);
								if(lvl.GetTile(x,y,z) == 2){
									addMob(lvl.name,"pig",nx,(ushort)((int)ny+80),nz,10);
									break;
								}
							}
						}
						if(mRand == 2 && vTime < 630000 && countMobsModel("sheep") < 50){
							for(ushort y = 110;y>10;y--){
								ushort nx = (ushort)(x*32);
								ushort ny = (ushort)(y*32);
								ushort nz = (ushort)(z*32);
								if(lvl.GetTile(x,y,z) == 2){
									addMob(lvl.name,"sheep",nx,(ushort)((int)ny+80),nz,8);
									break;
								}
							}
						}
					}
				}
				spawningTimeCounter = 0;
			}
				
		}
		
		void cycleDayNight(){
			vTime = vTime + 4;
			if (vTime > 1200000){
			vTime = 0;
			}
			
			Player[] online = PlayerInfo.Online.Items;
			foreach (Player pl in online){
				if(pl.level.name == hubMap) continue;
				if(vTime > 0 && vTime < 400000 && vLast != 0){//day
					pl.SendEnvColor(0,"-1"); //sky
					pl.SendEnvColor(1,"-1"); // cloud
					pl.SendEnvColor(2,"-1"); // fog
					pl.SendEnvColor(3,"-1"); // shadow
					pl.SendEnvColor(4,"-1"); // light
				}
				if((vTime > 400000 && vTime < 630000 && vLast != 1) || (vTime > 1140000 && vTime < 1200000 && vLast != 1)){ //sunset
					pl.SendEnvColor(0,"ffd190"); //sky
					pl.SendEnvColor(1,"c1b2b3"); // cloud
					pl.SendEnvColor(2,"c2a296"); // fog
					pl.SendEnvColor(3,"464652"); // shadow
					pl.SendEnvColor(4,"717073"); // light
				}
				if((vTime > 630000 && vTime < 1140000 && vLast != 2)){ //night
					pl.SendEnvColor(0,"070A23"); //sky
					pl.SendEnvColor(1,"2a2f59"); // cloud
					pl.SendEnvColor(2,"1E223A"); // fog
					pl.SendEnvColor(3,"2f2f3d"); // shadow
					pl.SendEnvColor(4,"515158"); // light
				}

			}
			if(vTime > 0 && vTime < 400000){
				vLast = 0;
			}
			if((vTime > 400000 && vTime < 630000) || (vTime > 1140000 && vTime < 1200000)){
				vLast = 1;
			}
			if((vTime > 630000 && vTime < 1140000)){
				vLast = 2;
			}

		}
		
		int gravityMobCounter = 0;
		void gravityMob(){
			gravityMobCounter++;
			if(gravityMobCounter > 2){
				for(int i = 0;i<150;i++){
					try{
						if(mobs[i,0] != null) {
							Position pos;
							pos.X = int.Parse(mobs[i,1]);
							pos.Y = int.Parse(mobs[i,2]);
							pos.Z = int.Parse(mobs[i,3]);
							
							Position tempPos = pos;
							tempPos.Y -= 51;
							Level lvl = LevelInfo.FindExact(mobs[i,5]);
							if(lvl != null){
								byte tileUnder = lvl.GetTile((ushort)pos.BlockX,(ushort)tempPos.BlockY,(ushort)pos.BlockZ);
								if(walkableList.Contains(tileUnder)){

									mobs[i,2] = (int.Parse(mobs[i,2]) - 3) + "";
									
									rot.RotY = (byte)int.Parse(mobs[i,6]);
									rot.HeadX = (byte)int.Parse(mobs[i,7]);
																
								}
							}
						}
					}catch (Exception ex){}
				}	
				gravityMobCounter = 0;
			}
		}
		
		int movementCounter = 0;
		Orientation eRot;
		Orientation eRot2;
		Orientation eRottrash;
		void movementsMob(){
			movementCounter++;
			if(movementCounter > 10){
				for(int i = 0;i<150;i++){
					try{
						if(mobs[i,0] == null) continue;
						
						Level lvl = LevelInfo.FindExact(mobs[i,5]);
						if(lvl == null) continue;
						
						Player p = getClosestFrom(mobs[i,5],int.Parse(mobs[i,1]),int.Parse(mobs[i,2]),int.Parse(mobs[i,3]));
						if(p == null) continue;
								
						double dist = (int)(Math.Sqrt(Math.Pow(Math.Abs(p.Pos.X - int.Parse(mobs[i,1])), 2) + Math.Pow(Math.Abs(p.Pos.Y - int.Parse(mobs[i,2])), 2) + Math.Pow(Math.Abs(p.Pos.Z - int.Parse(mobs[i,3])), 2)));
						if(dist < (mobs[i,0] == "zombie" ? 700 : 2000) && dist > 20){
							
							int mobX = int.Parse(mobs[i,1]);
							int mobY = int.Parse(mobs[i,2]);
							int mobZ = int.Parse(mobs[i,3]);
							
							int dx = p.Pos.X - mobX, dy = p.Pos.Y - mobY, dz = p.Pos.Z - mobZ;
							Vec3F32 dir = new Vec3F32(dx, dy, dz);
							dir = Vec3F32.Normalise(dir);						
							DirUtils.GetYawPitch(dir, out eRot.RotY, out eRot.HeadX);
							
							if(mobs[i,0] != "zombie") eRot.RotY = (byte)((int)eRot.RotY - 90);
							
							int tempdx = 0;
							int tempdz = 0;
							if(mobs[i,0] == "zombie"){
								tempdx = dx < 0 ? -1 : 1;
								tempdz = dz < 0 ? -1 : 1;
							}else if(mobs[i,8] == "1"){
								tempdx = dx < 0 ? 2 : -2;
								tempdz = dz < 0 ? 2 : -2;
							}else if(mobs[i,8] == "0"){
								tempdx = int.Parse(mobs[i,9]);
								tempdz = int.Parse(mobs[i,10]);
							}
							
							if(mobs[i,0] != "zombie"){	
								int dx2 = (mobX - (mobX - tempdx)), dz2 = (mobZ - (mobZ - tempdz));
								Vec3F32 dir2 = new Vec3F32(dx2, dy, dz2);
								dir2 = Vec3F32.Normalise(dir2);						
								DirUtils.GetYawPitch(dir2, out eRot2.RotY, out eRottrash.HeadX);
							}
							
							Position pos;
							pos.X = mobX;
							pos.Y = mobY;
							pos.Z = mobZ;
							
							Position tempPos = pos;
							tempPos.X += tempdx*20;
							tempPos.Z += tempdz*20;
							tempPos.Y -= 32;
							
							Position tempPos2 = pos;
							tempPos2.X += tempdx*20;
							tempPos2.Z += tempdz*20;
							
							Position tempPos3 = pos;
							tempPos3.Y -= 40;
							
							
							if(!walkableList.Contains(lvl.GetTile((ushort)tempPos3.BlockX,(ushort)tempPos3.BlockY,(ushort)tempPos3.BlockZ))){
								mobs[i,2] = (mobY + 16) + "";
							} else if(walkableList.Contains(lvl.GetTile((ushort)tempPos.BlockX,(ushort)tempPos.BlockY,(ushort)tempPos.BlockZ)) && walkableList.Contains(lvl.GetTile((ushort)tempPos.BlockX,(ushort)pos.BlockY,(ushort)tempPos.BlockZ))){
								mobs[i,1] = (mobX + tempdx) + "";
								mobs[i,3] = (mobZ + tempdz) + "";
							} else if(walkableList.Contains(lvl.GetTile((ushort)tempPos2.BlockX,(ushort)tempPos2.BlockY,(ushort)tempPos2.BlockZ))){
								mobs[i,2] = (mobY + 64) + "";
								mobs[i,1] = (mobX + tempdx) + "";
								mobs[i,3] = (mobZ + tempdz) + "";
							}
							mobs[i,6] = mobs[i,0] == "zombie" ? eRot.RotY + "" : eRot2.RotY + "";
							
							
						}						
					}catch (Exception ex){}
					
				}
				movementCounter = 0;
			}
		}
		
		int sendMobCounter = 0;
		void sendMobsPackets(){
			sendMobCounter++;
			if(sendMobCounter > 25){
				Player[] online = PlayerInfo.Online.Items;
				foreach (Player pl in online){
					if(pl.level.name == hubMap) continue;
					for(int i = 0;i<150;i++){
						try{
							if(mobs[i,0] == null) continue;
								Position pos;
								pos.X = int.Parse(mobs[i,1]);
								pos.Y = int.Parse(mobs[i,2]);
								pos.Z = int.Parse(mobs[i,3]);
								eRot.RotY = (byte)int.Parse(mobs[i,6]);
								pl.Send(Packet.Teleport((byte)(i+50), pos, eRot, true));
							
						}catch (Exception ex){}
				
					}
				}		
				sendMobCounter = 0;
			}	
				
		}
		
		int randomDirCounter = 0;
		int randomDirCounter2 = 0;
		void randomDirMob(){
			Random rnd = new Random();
			randomDirCounter++;
			randomDirCounter2++;
			if(randomDirCounter2 > 4000){
				try{
					for(int i = 0;i<150;i++){
						if(mobs[i,0] != null) {
							mobs[i,8] = "0";
						}
					}
				}catch (Exception ex){}
				randomDirCounter2 = 0;
			}
			if(randomDirCounter > 300){
				for(int i = 0;i<150;i++){
					if(mobs[i,0] != null) {
						try{
							
							int mRand = rnd.Next(-1,10);
							if(mRand == 0){
								
								mRand = rnd.Next(-1,2);
								mobs[i,9] = mRand + "";
								
								mRand = rnd.Next(-1,2);
								mobs[i,10] = mRand + "";
							}
						}catch (Exception ex){}
					}
				}
				randomDirCounter = 0;
			}
		}
		
		int attackMobCounter = 0;
		void attacksMob(){
			attackMobCounter++;
			if(attackMobCounter > 200){
				try
				{
					for(int j = 0;j<150;j++){
						if(mobs[j,0] == null || mobs[j,0] != "zombie") continue;
						int mobX = int.Parse(mobs[j,1]);
						int mobY = int.Parse(mobs[j,2]);
						int mobZ = int.Parse(mobs[j,3]);
						
						Player pl = getClosestFrom(mobs[j,5],mobX,mobY,mobZ);
						
						for(int i = 0;i<50;i++){
							if(playersInfo[i,0] == pl.name){
								double dist = (Math.Sqrt(Math.Pow(Math.Abs(pl.Pos.X - mobX), 2) + Math.Pow(Math.Abs(pl.Pos.Y - mobY), 2) + Math.Pow(Math.Abs(pl.Pos.Z - mobZ), 2)));	
								if(dist < 50) {
									playersInfo[i,1] = (int.Parse(playersInfo[i,1]) - 1) + "";
									setHpIndicator(i,pl);

									Position new_pos;
									new_pos.X = pl.Pos.X +(int)((pl.Pos.X-mobX)/2);
									new_pos.Y = pl.Pos.Y;
									new_pos.Z = pl.Pos.Z +(int)((pl.Pos.Z-mobZ)/2);

									Position new_midpos;
									new_midpos.X = pl.Pos.X +(int)((pl.Pos.X-mobX)/4);
									new_midpos.Y = pl.Pos.Y;
									new_midpos.Z = pl.Pos.Z +(int)((pl.Pos.Z-mobZ)/4);

									if (pl.level.IsAirAt((ushort)new_pos.BlockX, (ushort)new_pos.BlockY, (ushort)new_pos.BlockZ) && pl.level.IsAirAt((ushort)new_pos.BlockX, (ushort)(new_pos.BlockY-1), (ushort)new_pos.BlockZ) && 
									pl.level.IsAirAt((ushort)new_midpos.BlockX, (ushort)new_midpos.BlockY, (ushort)new_midpos.BlockZ) && pl.level.IsAirAt((ushort)new_midpos.BlockX, (ushort)(new_midpos.BlockY-1), (ushort)new_midpos.BlockZ)){
									if(pl.Pos.BlockX != pl.level.SpawnPos.BlockX && pl.Pos.BlockY != pl.level.SpawnPos.BlockY && pl.Pos.BlockY != pl.level.SpawnPos.BlockZ){pl.SendPos(Entities.SelfID, new Position(new_pos.X, new_pos.Y, new_pos.Z), pl.Rot);}
									}


									if(int.Parse(playersInfo[i,1]) < 1){
									playersInfo[i,1] = "20";
									pl.SendPos(Entities.SelfID, new Position(pl.Level.SpawnPos.X, pl.Level.SpawnPos.Y, pl.Level.SpawnPos.Z), pl.Rot);     
									Chat.MessageLevel(pl.level, pl.ColoredName + " %Swas slain by a " + mobs[j,0] + "."); 
									pl.SendCpeMessage(CpeMessageType.Status1, "%c♥♥♥♥♥♥♥♥♥♥");
									}
								}
							}
						}
						
					}
				} catch(Exception e){}
			attackMobCounter = 0;
			}
		}
		
		void mobAloneRespawning(){
		try
		{
			for(int i = 0;i<150;i++){
				if(mobs[i,0] != null){
					bool foundAPlayer = false;
					Level lvl = LevelInfo.FindExact(mobs[i,5]);
					if(lvl != null){
						Player[] online = PlayerInfo.Online.Items;		
						foreach (Player pl in online){
							if(pl.level.name == lvl.name){
								if(mobs[i,1]!= null && mobs[i,2]!= null && mobs[i,3]!= null){
									int x = int.Parse(mobs[i,1]);
									int y = int.Parse(mobs[i,2]);
									int z = int.Parse(mobs[i,3]);
									int dist = (int)(Math.Sqrt(Math.Pow(Math.Abs(x - pl.Pos.X), 2) + Math.Pow(Math.Abs(y - pl.Pos.Y), 2) + Math.Pow(Math.Abs(z - pl.Pos.Z), 2)));
									if (dist < 3500) {
										foundAPlayer = true;
									}
								}
							}
						}
					}
					if(!foundAPlayer) removeMob(i,mobs[i,5]);
				}
			}
		} catch(Exception e){}
		}
		
		int refreshCounter = 0;
		void refreshUI(){
			refreshCounter++;
			if(refreshCounter > 75){
				createConnectedList();
				refreshCounter = 0;
				Player[] online = PlayerInfo.Online.Items;
				foreach (Player pl in online){
					byte block_ID = (pl.GetHeldBlock().BlockID == 163 ? pl.GetHeldBlock().ExtID : pl.GetHeldBlock().BlockID);
					
					
					string blockName = getBlockQuantity(pl,block_ID) != "0" ? "%e" + getExtName(block_ID) + " ->" : "%cOut of " + getExtName(block_ID) + " ! ";
					if(blockName == "%cOut of Air ! " || blockName == "%cOut of air ! ") blockName = "";
					
					string quantity =  getBlockQuantity(pl,block_ID) != "0" ? "%eQuantity: " + getBlockQuantity(pl,block_ID) : "";
					string durability = getBlockDurability(pl,block_ID) != "0" ? "%eDurability: " + getBlockDurability(pl,block_ID) : "";
					
					pl.SendCpeMessage(CpeMessageType.BottomRight3,  blockName);
					pl.SendCpeMessage(CpeMessageType.BottomRight2,  quantity);
					pl.SendCpeMessage(CpeMessageType.BottomRight1,  durability);
					for(int i = 0;i<50;i++){
						if(playersInfo[i,0] == pl.name){
							setHpIndicator(i,pl);
							if(isInventoryFull(i)) pl.SendCpeMessage(CpeMessageType.Status3, "%4YOUR INVENTORY IS FULL !"); //To fix
						} 
					}
				}
			}
		}
		
		int breathCounter = 0;
		int lavaCounter = 0;
		void playerInsideBlock(){
			breathCounter++;
			lavaCounter++;
			if(breathCounter > 350){
				for(int i = 0;i<50;i++){
						Player[] online = PlayerInfo.Online.Items;
						foreach (Player pl in online){
							if(pl.name == playersInfo[i,0]){
								Level lvl = pl.Level;
								if(lvl.GetTile((ushort)pl.Pos.BlockX,(ushort)pl.Pos.BlockY,(ushort)pl.Pos.BlockZ) == 8 || lvl.GetTile((ushort)pl.Pos.BlockX,(ushort)pl.Pos.BlockY,(ushort)pl.Pos.BlockZ) == 9){
									playersInfo[i,3] = (int.Parse(playersInfo[i,3])-1) + "";
								} else {
									playersInfo[i,3] = "20";
								}
								

								
								if(int.Parse(playersInfo[i,3]) < 1){
									playersInfo[i,1] = (int.Parse(playersInfo[i,1])-1) + "";
									if(int.Parse(playersInfo[i,1]) < 1){
										playersInfo[i,1] = "20";
										pl.HandleCommand("spawn","");     
										Chat.MessageLevel(pl.level, pl.ColoredName + " %Sdrowned."); 
									}
								}
							}
						}	
				}
			breathCounter = 0;
			}
			
			if(lavaCounter > 100){
				for(int i = 0;i<50;i++){
					Player[] online = PlayerInfo.Online.Items;
					foreach (Player pl in online){
						if(pl.name == playersInfo[i,0]){
							Level lvl = pl.Level;
							if(lvl.GetTile((ushort)pl.Pos.BlockX,(ushort)(pl.Pos.BlockY-1),(ushort)pl.Pos.BlockZ) == 10 || lvl.GetTile((ushort)pl.Pos.BlockX,(ushort)(pl.Pos.BlockY-1),(ushort)pl.Pos.BlockZ) == 11){
								playersInfo[i,1] = (int.Parse(playersInfo[i,1])-1) + "";
								if(int.Parse(playersInfo[i,1]) < 1){
									playersInfo[i,1] = "20";
									pl.HandleCommand("spawn","");     
									Chat.MessageLevel(pl.level, pl.ColoredName + " %Stried to swim in lava."); 
								}
							} 
						}
					}
				}
				lavaCounter = 0;
			}
		}
		
		void playerFallDamage(){
			for(int i = 0;i<50;i++){
				Player[] online = PlayerInfo.Online.Items;
				foreach (Player pl in online){
					if(pl.name == playersInfo[i,0]){
							if(!pl.level.IsAirAt((ushort)pl.Pos.BlockX,(ushort)(pl.Pos.BlockY-2),(ushort)pl.Pos.BlockZ)){
								if(pl.level.GetTile((ushort)pl.Pos.BlockX,(ushort)(pl.Pos.BlockY-2),(ushort)pl.Pos.BlockZ) != 8 && pl.level.GetTile((ushort)pl.Pos.BlockX,(ushort)(pl.Pos.BlockY-2),(ushort)pl.Pos.BlockZ) != 9){
									if(pl.Pos.BlockY <= (int)pl.Level.Height){
									
										double FallDamage = ((int.Parse(playersInfo[i,4]) - (ushort)(pl.Pos.BlockY-2))*0.5)-1.5;
										
										int a = (int)(int.Parse(playersInfo[i,5]) - pl.Pos.BlockX);
										int b = (int)(int.Parse(playersInfo[i,6]) - pl.Pos.BlockY);
										int dist =  (int)Math.Sqrt(a * a + b * b);
										
										if(FallDamage < 1 || dist > 15) FallDamage = 0;
										
										if(FallDamage < 10000){
											playersInfo[i,1] = (int.Parse(playersInfo[i,1]) - (int)FallDamage) + "";
										}
										
										playersInfo[i,4] = (ushort)(pl.Pos.BlockY-2) + "";
										playersInfo[i,5] = pl.Pos.BlockX + "";
										playersInfo[i,6] = pl.Pos.BlockY + "";
										if(int.Parse(playersInfo[i,1]) < 1){
											playersInfo[i,4] = "0";
											playersInfo[i,1] = "20";
											pl.HandleCommand("spawn","");     
											Chat.MessageLevel(pl.level, pl.ColoredName + " %Sfell from a high place."); 
										}
									}
								} else { playersInfo[i,4] = "0"; }	
							}
					}
				}
			}
		}
			
		void mobMoveScheduler(){
			try{
				while (ThreadNotDead){
					Thread.Sleep(1);	
					gravityMob();
					randomDirMob();
					movementsMob();
				}
			}
			catch(ThreadAbortException){
				Thread.ResetAbort();
			}
		}
			
		void mobScheduler(){
			try{
				while (ThreadNotDead){
					Thread.Sleep(1);	
					spawningMob();
					attacksMob();
					mobAloneRespawning();
					sendMobsPackets();
				}
			}
			catch(ThreadAbortException){
				Thread.ResetAbort();
			}
		}
		
		void scheduler(){
			try{
				while (ThreadNotDead){
					Thread.Sleep(1);	
					cycleDayNight();
					playerInsideBlock();
					playerFallDamage();
					refreshUI();				
				}
			}
			catch(ThreadAbortException){
				Thread.ResetAbort();
			}
		}
		
		Player cp;
		Player getClosestFrom(string lvl,int x,int y,int z){
			int cdistance = 1000000;
	
			Player[] online = PlayerInfo.Online.Items;		
			foreach (Player pl in online){
				if(pl.level.name == lvl){
					int dist = (int)(Math.Sqrt(Math.Pow(Math.Abs(x - pl.Pos.X), 2) + Math.Pow(Math.Abs(y - pl.Pos.Y), 2) + Math.Pow(Math.Abs(z - pl.Pos.Z), 2)));
					if (dist < cdistance) {
						cdistance = dist; 
						cp = pl;
					}
				}
			}
			return cp;
		}
		
		void refreshMobs(Player p){
			while (Thread.CurrentThread.IsAlive){
				try
				{
					Thread.Sleep(1000);			
					for(int i = 0;i<150;i++){
						if(mobs[i,0] != null) {
							
							Position pos;
							pos.X = int.Parse(mobs[i,1]);
							pos.Y = int.Parse(mobs[i,2]);
							pos.Z = int.Parse(mobs[i,3]);
							
							Player[] online = PlayerInfo.Online.Items;
							foreach (Player pl in online){
								if(pl.level.name == hubMap) continue;
								if(pl.level.name == mobs[i,5]){
									pl.Send(Packet.AddEntity((byte)(i+50),"",pos,rot,true,true));
									pl.Send(Packet.ChangeModel((byte)(i+50), mobs[i,0], true));
								}
							}
						}
					}

					RefreshCycleDayNight(p);
					mobLoadThread.Abort();
				} catch(Exception e){}
			}
			
		}
		
		void addMob(string lvl,string model,int x, int y, int z, int hp){
			for(int i = 0;i<150;i++){
				if(mobs[i,0] == null) {
						
					mobs[i,0] = model;
					mobs[i,1] = x + "";
					mobs[i,2] = y + "";
					mobs[i,3] = z + "";
					mobs[i,4] = hp + "";
					mobs[i,5] = lvl;
					mobs[i,6] = "0";
					mobs[i,7] = "0";
					mobs[i,8] = "0"; //AI State
					mobs[i,9] = "0"; //rx 
					mobs[i,10] = "0";//ry
					
					Position pos;
					pos.X = x;
					pos.Y = y;
					pos.Z = z;
								
					Player[] online = PlayerInfo.Online.Items;
					
					foreach (Player pl in online){
						if(pl.level.name == hubMap) continue;
						if(pl.level.name == lvl){
							pl.Send(Packet.AddEntity((byte)(i+50),"",pos,rot,true,true));
							pl.Send(Packet.ChangeModel((byte)(i+50), model, true));
						}
					}
					return;
				}
			}
		}
		
		void removeMob(int id,string lvl){
			mobs[id,0] = null;
			mobs[id,1] = null;
			mobs[id,2] = null;
			mobs[id,3] = null;
			mobs[id,4] = null;
			mobs[id,6] = null;
			mobs[id,7] = null;
			mobs[id,8] = null;
			mobs[id,9] = null;
			mobs[id,10] = null;
			
			Player[] online = PlayerInfo.Online.Items;
			foreach (Player pl in online){
				if(pl.level.name == hubMap) continue;
				pl.Send(Packet.RemoveEntity((byte)(id+50)));
			}
		}

		int whatArmor(byte b){
			switch(b){
				case 94 :
					return 0;
				case 95 :
					return 1;
				case 96 :
					return 2 ;
				case 97 :
					return 3;
				default :
					return -1;
			}
		}
			
		void refreshArmors(Player p){
				Thread.Sleep(2000);
				p.cancelcommand = true;
				p.SkinName = "https://sirvoid.000webhostapp.com/" + p.truename + ".png";
				Player[] online = PlayerInfo.Online.Items;
				Entities.GlobalDespawn(p, true);
				Entities.GlobalSpawn(p, true);
				return;
		}
			
		void HandleCommand(Player p, string cmd, string arg) {
			string[] args = arg.Split(' ');										
			int indexH = 0;
			for(int i = 0;i<50;i++){
				if(playersInv[i,0,0] == p.name){
					indexH = i;
				}
			}
			if (cmd == "craft"){
				p.cancelcommand = true;
				if(arg == ""){
					Player.Message(p,"%a/craft [item name] [multiplier]%e - craft an item with the resources you have. ");
					Player.Message(p,"%eTo see the list of available crafts, type %a/craftlist");
				} else {
					if(isInventoryFull(indexH)){ 
						Player.Message(p,"%cYour inventory is full !");
						return;
					}

					int multiCrafted = 1;
					if(args.Length > 1){
					if(int.Parse(args[1]) > 0) multiCrafted = int.Parse(args[1]);
					}
					switch(args[0].ToLower()){
						case "plank": //Wood Craft
							if(!haveBlock(p,17,1*multiCrafted)) break;
							addToInv(indexH,5,4*multiCrafted,0);
							subToInv(indexH,p,17,1*multiCrafted);
							Player.Message(p,"%eYou crafted 4 Planks." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "slab": //Slab Craft
							if(!haveBlock(p,4,3*multiCrafted)) break;
							addToInv(indexH,44,6*multiCrafted,0);
							subToInv(indexH,p,4,3*multiCrafted);
							Player.Message(p,"%eYou crafted 6 Slabs." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return; 
						case "glass": //glass Craft
							if(!haveBlock(p,12,8*multiCrafted)) break;
							if(!haveBlock(p,16,1*multiCrafted)) break;
							addToInv(indexH,20,8*multiCrafted,0);
							subToInv(indexH,p,12,8*multiCrafted);
							subToInv(indexH,p,16,1*multiCrafted);
							Player.Message(p,"%eYou crafted 8 Glass." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "stick": //stick Craft
							if(!haveBlock(p,5,2*multiCrafted)) break;
							addToInv(indexH,67,4*multiCrafted,0);
							subToInv(indexH,p,5,2*multiCrafted);
							Player.Message(p,"%eYou crafted 4 Sticks." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "sapling": //stick Craft
							if(!haveBlock(p,18,10*multiCrafted)) break;
							addToInv(indexH,6,1*multiCrafted,0);
							subToInv(indexH,p,18,10*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Sapling." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "woodpickaxe": //Wood Pickaxe Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,5,3*multiCrafted)) break;
							addToInv(indexH,66,1*multiCrafted,33);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,5,3*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Wood Pickaxe." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "woodaxe": //Wood Axe Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,5,3*multiCrafted)) break;
							addToInv(indexH,68,1*multiCrafted,33);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,5,3*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Wood Axe." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "woodshovel": //Wood Shovel Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,5,1*multiCrafted)) break;
							addToInv(indexH,69,1*multiCrafted,33);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,5,1*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Wood Shovel." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "stonepickaxe": //Stone Pickaxe Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,4,3*multiCrafted)) break;
							addToInv(indexH,70,1*multiCrafted,65);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,4,3*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Stone Pickaxe." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "stoneaxe": //Stone Pickaxe Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,4,3*multiCrafted)) break;
							addToInv(indexH,71,1*multiCrafted,65);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,4,3*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Stone Axe." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "stoneshovel": //Stone Pickaxe Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,4,1*multiCrafted)) break;
							addToInv(indexH,72,1*multiCrafted,65);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,4,1*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Stone Shovel." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "ironpickaxe": //Stone Pickaxe Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,42,3*multiCrafted)) break;
							addToInv(indexH,73,1*multiCrafted,129);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,42,3*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Iron Pickaxe." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "ironaxe": //Stone Pickaxe Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,42,3*multiCrafted)) break;
							addToInv(indexH,74,1*multiCrafted,129);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,42,3*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Iron Axe." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "ironshovel": //Stone Pickaxe Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,42,1*multiCrafted)) break;
							addToInv(indexH,75,1*multiCrafted,129);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,42,1*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Iron Shovel." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "door": //door Pickaxe Craft
							if(!haveBlock(p,5,6*multiCrafted)) break;
							addToInv(indexH,78,1*multiCrafted,0);
							subToInv(indexH,p,5,6*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Door." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "bookshelf": //bookshelf Craft
							if(!haveBlock(p,5,6*multiCrafted)) break;
							if(!haveBlock(p,18,3*multiCrafted)) break;
							addToInv(indexH,47,3*multiCrafted,0);
							subToInv(indexH,p,5,6*multiCrafted);
							subToInv(indexH,p,18,3*multiCrafted);
							Player.Message(p,"%eYou crafted 3 Bookshelf." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "mossyrock": //bookshelf Craft
							if(!haveBlock(p,4,3*multiCrafted)) break;
							if(!haveBlock(p,18,3*multiCrafted)) break;
							addToInv(indexH,48,3*multiCrafted,0);
							subToInv(indexH,p,4,3*multiCrafted);
							subToInv(indexH,p,18,3*multiCrafted);
							Player.Message(p,"%eYou crafted 3 Mossy Cobblestone." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "rope": //rope Craft
							if(!haveBlock(p,18,6*multiCrafted)) break;
							addToInv(indexH,51,3*multiCrafted,0);
							subToInv(indexH,p,18,6*multiCrafted);
							Player.Message(p,"%eYou crafted 3 Ropes." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "torch": //torch Craft
							if(!haveBlock(p,67,1*multiCrafted)) break;
							if(!haveBlock(p,16,1*multiCrafted)) break;
							addToInv(indexH,88,4*multiCrafted,0);
							subToInv(indexH,p,67,1*multiCrafted);
							subToInv(indexH,p,16,1*multiCrafted);
							Player.Message(p,"%eYou crafted 3 Torches." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;	
						case "stone": //stone Craft
							if(!haveBlock(p,4,3*multiCrafted)) break;
							if(!haveBlock(p,16,3*multiCrafted)) break;
							addToInv(indexH,1,3*multiCrafted,0);
							subToInv(indexH,p,4,3*multiCrafted);
							subToInv(indexH,p,16,3*multiCrafted);
							Player.Message(p,"%eYou crafted 3 Stones." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "brick": //brick Craft
							if(!haveBlock(p,13,6*multiCrafted)) break;
							addToInv(indexH,45,3*multiCrafted,0);
							subToInv(indexH,p,13,6*multiCrafted);
							Player.Message(p,"%eYou crafted 3 Bricks." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "woodsword": //woordsword Craft
							if(!haveBlock(p,67,1*multiCrafted)) break;
							if(!haveBlock(p,5,2*multiCrafted)) break;
							addToInv(indexH,86,1*multiCrafted,33);
							subToInv(indexH,p,67,1*multiCrafted);
							subToInv(indexH,p,5,2*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Wood Sword." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "stonesword": //stonesword Craft
							if(!haveBlock(p,67,1*multiCrafted)) break;
							if(!haveBlock(p,4,2*multiCrafted)) break;
							addToInv(indexH,87,1*multiCrafted,65);
							subToInv(indexH,p,67,1*multiCrafted);
							subToInv(indexH,p,4,2*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Stone Sword." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;	
						case "porkchop": //cookedporkchop Craft
							if(!haveBlock(p,89,1*multiCrafted)) break;
							if(!haveBlock(p,16,1*multiCrafted)) break;
							addToInv(indexH,90,1*multiCrafted,0);
							subToInv(indexH,p,89,1*multiCrafted);
							subToInv(indexH,p,16,1*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Porkchop." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "crate": //crate Craft
							if(!haveBlock(p,5,20*multiCrafted)) break;
							addToInv(indexH,64,1*multiCrafted,0);
							subToInv(indexH,p,5,20*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Crate." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;	
						case "bucket": //bucket Craft
							if(!haveBlock(p,42,5*multiCrafted)) break;
							addToInv(indexH,92,1*multiCrafted,0);
							subToInv(indexH,p,42,5*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Bucket." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;	
						case "ironsword": //ironsword Craft
							if(!haveBlock(p,67,1*multiCrafted)) break;
							if(!haveBlock(p,42,2*multiCrafted)) break;
							addToInv(indexH,98,1*multiCrafted,129);
							subToInv(indexH,p,67,1*multiCrafted);
							subToInv(indexH,p,42,2*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Iron Sword." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "diamondpickaxe": //diamond Pickaxe Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,91,3*multiCrafted)) break;
							addToInv(indexH,99,1*multiCrafted,257);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,91,3*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Diamond Pickaxe." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "diamondaxe": //Diamond Axe Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,91,3*multiCrafted)) break;
							addToInv(indexH,100,1*multiCrafted,257);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,91,3*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Diamond Axe." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "diamondshovel": //Diamond shovel Craft
							if(!haveBlock(p,67,2*multiCrafted)) break;
							if(!haveBlock(p,91,1*multiCrafted)) break;
							addToInv(indexH,101,1*multiCrafted,257);
							subToInv(indexH,p,67,2*multiCrafted);
							subToInv(indexH,p,91,1*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Diamond Shovel." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "diamondsword": //diamondsword Craft
							if(!haveBlock(p,67,1*multiCrafted)) break;
							if(!haveBlock(p,91,2*multiCrafted)) break;
							addToInv(indexH,102,1*multiCrafted,257);
							subToInv(indexH,p,67,1*multiCrafted);
							subToInv(indexH,p,91,2*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Diamond Sword." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;	
						case "red": //crate Craft
							if(!haveBlock(p,36,1*multiCrafted)) break;
							if(!haveBlock(p,38,1*multiCrafted)) break;
							addToInv(indexH,21,1*multiCrafted,0);
							subToInv(indexH,p,36,1*multiCrafted);
							subToInv(indexH,p,38,1*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Red Wool." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "yellow": //crate Craft
							if(!haveBlock(p,36,1*multiCrafted)) break;
							if(!haveBlock(p,37,1*multiCrafted)) break;
							addToInv(indexH,23,1*multiCrafted,0);
							subToInv(indexH,p,36,1*multiCrafted);
							subToInv(indexH,p,37,1*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Yellow Wool." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "green": //crate Craft
							if(!haveBlock(p,36,1*multiCrafted)) break;
							if(!haveBlock(p,6,1*multiCrafted)) break;
							addToInv(indexH,25,1*multiCrafted,0);
							subToInv(indexH,p,36,1*multiCrafted);
							subToInv(indexH,p,6,1*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Green Wool." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;
						case "cyan": //crate Craft
							if(!haveBlock(p,36,1*multiCrafted)) break;
							if(!haveBlock(p,93,1*multiCrafted)) break;
							addToInv(indexH,28,1*multiCrafted,0);
							addToInv(indexH,92,1*multiCrafted,0);
							subToInv(indexH,p,36,1*multiCrafted);
							subToInv(indexH,p,93,1*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Cyan Wool." + " (x" + multiCrafted + ")");
							Player.Message(p,"%eAnd got your bucket back.");
							refreshInvOrder(p,indexH);
							return;
						case "bed": //bed Craft
							if(!haveBlock(p,36,3*multiCrafted)) break;
							if(!haveBlock(p,5,3*multiCrafted)) break;
							addToInv(indexH,103,1*multiCrafted,0);
							subToInv(indexH,p,36,3*multiCrafted);
							subToInv(indexH,p,5,3*multiCrafted);
							Player.Message(p,"%eYou crafted 1 Bed." + " (x" + multiCrafted + ")");
							refreshInvOrder(p,indexH);
							return;								
						default: //Unknown Craft
							Player.Message(p,"%cUnknown Item " + "\"" + args[0]  + "\"" + " .");
							return;
					}
					Player.Message(p,"%cNot enough resources for this craft.");
				}
			}
			else if (cmd == "craftlist"){
				p.cancelcommand = true;		
				if(arg == ""){
					Player.Message(p,"%a/craftlist [category]");
					Player.Message(p,"%aAvailable Categories:");
					Player.Message(p,"%a-%eMisc");
					Player.Message(p,"%a-%eMaterials");
					Player.Message(p,"%a-%eBlocks");
					Player.Message(p,"%a-%eTools");
					return;
				}
				Player.Message(p,"%a[Item Name] %e- [Resources you need]");
				Player.Message(p,"%eCrafts of this category:");
				if(arg.ToLower() == "misc"){
					Player.Message(p,"%aSapling %e - Leaves(10x) ");
					Player.Message(p,"%aTorch %e - Stick(1x) Coal(1x)");
					Player.Message(p,"%aPorkchop %e - RawPorkchop(1x) Coal(1x)");
					Player.Message(p,"%aBed %e - Plank(3x) White(3x)");
				}
				if(arg.ToLower() == "materials"){
					Player.Message(p,"%aStick %e - Plank(2x) ");
				}
				if(arg.ToLower() == "blocks"){
					Player.Message(p,"%aPlank %e - Log(1x) ");
					Player.Message(p,"%aSlab %e - Cobblestone(3x) ");
					Player.Message(p,"%aGlass %e - Sand(8x) Coal(1x) ");
					Player.Message(p,"%aDoor %e - Plank(6x)");
					Player.Message(p,"%aBookshelf %e - Plank(6x) Leaves(3x)");
					Player.Message(p,"%aRope %e - Leaves(6x)");
					Player.Message(p,"%aMossyRock %e - Cobblestone(3x) Leaves(3x)");
					Player.Message(p,"%aStone %e - Cobblestone(3x) Coal(3x)");
					Player.Message(p,"%aBrick %e - Gravel(6x)");
					Player.Message(p,"%aCrate %e - Planks(20x)");
					Player.Message(p,"%aRed %e - White(1x) Rose(1x)");
					Player.Message(p,"%aYellow %e - White(1x) Dandelion(1x)");
					Player.Message(p,"%aCyan %e - White(1x) WaterBucket(1x)");
					Player.Message(p,"%aGreen %e - White(1x) Sapling(1x)");
				}
				if(arg.ToLower() == "tools"){
					Player.Message(p,"%aWoodPickaxe %e - Plank(3x) Stick(2x) ");
					Player.Message(p,"%aWoodAxe %e - Plank(3x) Stick(2x) ");
					Player.Message(p,"%aWoodShovel %e - Plank(1x) Stick(2x) ");
					Player.Message(p,"%aWoodSword %e - Plank(2x) Stick(1x) ");
					Player.Message(p,"%aStonePickaxe %e - Cobblestone(3x) Stick(2x) ");
					Player.Message(p,"%aStoneAxe %e - Cobblestone(3x) Stick(2x) ");
					Player.Message(p,"%aStoneShovel %e - Cobblestone(1x) Stick(2x) ");
					Player.Message(p,"%aStoneSword %e - Cobblestone(2x) Stick(1x) ");
					Player.Message(p,"%aIronPickaxe %e - Iron(3x) Stick(2x) ");
					Player.Message(p,"%aIronAxe %e - Iron(3x) Stick(2x) ");
					Player.Message(p,"%aIronShovel %e - Iron(1x) Stick(2x) ");
					Player.Message(p,"%aIronSword %e - Iron(2x) Stick(1x) ");
					Player.Message(p,"%aDiamondPickaxe %e - Diamond(3x) Stick(2x) ");
					Player.Message(p,"%aDiamondAxe %e - Diamond(3x) Stick(2x) ");
					Player.Message(p,"%aDiamondShovel %e - Diamond(1x) Stick(2x) ");
					Player.Message(p,"%aDiamondSword %e - Diamond(2x) Stick(1x) ");
					Player.Message(p,"%aBucket %e - Iron(5x)");
				}
			}
			else if (cmd == "inventory"){ 
				p.cancelcommand = true;
				showInventory(p);
			}
			else if (cmd == "drop"){ 
				p.cancelcommand = true;
				byte b_ID = (p.GetHeldBlock().BlockID == 163 ? p.GetHeldBlock().ExtID : p.GetHeldBlock().BlockID);	
				if(int.Parse(args[0]) > 0 && haveBlock(p,b_ID,int.Parse(args[0]))){
					subToInv(indexH,p,b_ID,int.Parse(args[0]));
					refreshInvOrder(p,indexH);
					Player.Message(p,"%cDropped " + args[0] + " " + getExtName(b_ID) + ".");
				}
				
			}
			else if (cmd == "equip"){
				p.cancelcommand = true;
				byte b_ID = (p.GetHeldBlock().BlockID == 163 ? p.GetHeldBlock().ExtID : p.GetHeldBlock().BlockID);
				if(!haveBlock(p,b_ID,1) || whatArmor(b_ID) == -1 || playersInv[indexH,(30+whatArmor(b_ID)),0] != "0") return;
				subToInv(indexH,p,b_ID,1);
				refreshInvOrder(p,indexH);
				playersInv[indexH,(30+whatArmor(b_ID)),0] = b_ID + "";
				playersInv[indexH,(30+whatArmor(b_ID)),1] = "1";
				saveInv(p);
				createConnectedList();
				Thread equipLoadThread = new Thread(() => refreshArmors(p));
				equipLoadThread.Start();
			}else if (cmd == "unequip"){
				p.cancelcommand = true;
				byte indexArmor = (byte)(30+int.Parse(args[0]));
				if(indexArmor < 30 && indexArmor > 33) return;
				addToInv(indexH,byte.Parse(playersInv[indexH,indexArmor,0]),1,0);
				refreshInvOrder(p,indexH);
				playersInv[indexH,indexArmor,0] = "0";
				playersInv[indexH,indexArmor,0] = "0";
				saveInv(p);
				createConnectedList();
				Thread equipLoadThread = new Thread(() => refreshArmors(p));
				equipLoadThread.Start();
			}
			else if (cmd == "dg"){ 
				
				Player.Message(p,vTime + "");
				
			}
			else if (cmd == "creative"){ 
				p.cancelcommand = true;
				if(p.Rank >= LevelPermission.Operator && creativePlayer != p.name){
					creativePlayer = p.name;
				}else if(creativePlayer == p.name){
					creativePlayer = "";
				}
				
			}
			else if(cmd == "home"){
				p.cancelcommand = true;
				string homeString = loadHome(p);
				if(homeString == ""){ 
					Player.Message(p,"%eRight click on a bed to set your home!");
					return;
				}
				string[] homeInfo = homeString.Split(',');
				if(p.level.name != homeInfo[0]){ Player.Message(p,"%eYour home is not in this world!"); return;}
				p.SendPos(Entities.SelfID, new Position((ushort)int.Parse(homeInfo[1])*32, (ushort)(int.Parse(homeInfo[2])+2)*32, (ushort)int.Parse(homeInfo[3])*32), p.Rot);
			}
			else if(cmd == "cp"){
				byte b_ID = (p.GetHeldBlock().BlockID == 163 ? p.GetHeldBlock().ExtID : p.GetHeldBlock().BlockID);	
				chestPlaceItem(p,b_ID,1,0);
			}
		}
		
		string getExtName(byte b){
				switch(b){
				case 246:
				case 247:
				case 248:
				case 249:
				case 250:
				case 251:
				case 252:
				case 253:
				case 254:
					return "air";
				case 66:
					return "WoodPickaxe";
				case 67:
					return "Stick";
				case 68:
					return "WoodAxe";
				case 69:
					return "WoodShovel";
				case 70:
					return "StonePickaxe";
				case 71:
					return "StoneAxe";
				case 72:
					return "StoneShovel";
				case 73:
					return "IronPickaxe";
				case 74:
					return "IronAxe";
				case 75:
					return "IronShovel";
				case 78:					
				case 79:
				case 80:
				case 81:
				case 82:
				case 83:
				case 84:
				case 85:
					return "Door";
				case 86:
					return "WoodSword";
				case 87:
					return "StoneSword";
				case 88:
					return "Torch";
				case 91:
					return "Diamond";
				case 76:
					return "Diamond_Ore";
				case 77:
					return "Redstone_Ore";
				case 89:
					return "RawPorkchop";
				case 90:
					return "Porkchop";
				case 92:
					return "Bucket";
				case 93:
					return "WaterBucket";	
				case 98:
					return "IronSword";		
				case 99:
					return "DiamondPickaxe";	
				case 100:
					return "DiamondAxe";
				case 101:
					return "DiamondShovel";
				case 102:
					return "DiamondSword";	
				case 103:
				case 104:
				case 105:
				case 106:
				case 107:
				case 108:
				case 109:
				case 110:
					return "Bed";
				case 111:
				case 112:
				case 113:
				case 114:
					return "Chest";
				default:
					return Block.Name(b);
			}
		}
		
		void showInventory(Player p){
			string invString = "";
			for(int i = 0;i<50;i++){
				if(playersInv[i,0,0] == p.name){
					for(int j = 1;j<30;j++){
						if(Block.Name((byte)(int.Parse(playersInv[i,j,0]))) != "Air"){
							invString += " %e" + getExtName((byte)(int.Parse(playersInv[i,j,0]))) + "%f[" + playersInv[i,j,1] + "] " + (int.Parse(playersInv[i,j,2]) == 0 ? null : "%a[" + playersInv[i,j,2] + "] ");
						}
					}
					Player.Message(p, invString);
				}
			}
		}
		
		
		public override void Help(Player p)
		{
			Player.Message(p,"%7-----%aSurvival Plugin by Sirvoid%7-----");
			Player.Message(p,"%eFor more informations about crafting, type %a/craft");
			Player.Message(p,"%eTo see your inventory, type %a/inventory");
			Player.Message(p,"%eTo delete items that you're holding, %a/drop <quantity>");
			Player.Message(p,"%eTo go home, %a/home");
			Player.Message(p,"%7-------------------------------");
			
		}
	}
}