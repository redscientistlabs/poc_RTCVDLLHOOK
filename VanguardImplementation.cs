﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using OpenGL;
using EasyHook;
using RTCV.Common;
using RTCV.CorruptCore;
using RTCV.NetCore;
using RTCV.Vanguard;

namespace XemuVanguardHook
{
	public class MemoryDomainCPUMemory : IMemoryDomain
    {
		public string Name { get; }
		public bool BigEndian => false;
		public long Size { get; }
		public int WordSize => 4;

        public override string ToString()
        {
			return Name;
        }
		public MemoryDomainCPUMemory()
        {
			Name = "Main CPU";
			Size = 128 * 1024 * 1024; //make the size 128MB for now; we are just testing this on xemu at the moment
        }
		public void PokeByte(long address, byte data)
        {
			if(address > Size)
            {
				return;
            }
			VanguardImplementation.GPA_WRITEB(Convert.ToUInt32(address), data);
        }
		public byte PeekByte(long address)
        {
			if(address > Size)
            {
				return 0;
            }
			byte buffer = 0;
			VanguardImplementation.GPA_READB(Convert.ToUInt32(address), buffer);
			return buffer;
        }
		public byte[] PeekBytes(long address, int length)
        {

			var returnArray = new byte[length];
			for (var i = 0; i < length; i++)
				returnArray[i] = PeekByte(address + i);
			return returnArray;
		}
    }
    public class VanguardImplementation
    {

        [DllImport("xemu.exe", EntryPoint = "gpa_readb")]
        public static extern void GPA_READB(uint addr, uint buf);
        [DllImport("xemu.exe", EntryPoint = "gpa_writeb")]
        public static extern void GPA_WRITEB(uint addr, uint buf);
        //[UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        //delegate
        public static RTCV.Vanguard.VanguardConnector connector = null;
        public static void StartClient()
        {
            try
            {
                ConsoleEx.WriteLine("Starting Vanguard Client");
                Thread.Sleep(500);
                var spec = new NetCoreReceiver();
                spec.Attached = VanguardCore.attached;
                spec.MessageReceived += OnMessageRecieved;
                connector = new RTCV.Vanguard.VanguardConnector(spec);
            }
            catch (Exception ex)
            {
                ConsoleEx.WriteLine("ERROR");
                throw new RTCV.NetCore.AbortEverythingException();
            }
        }
        public static void RestartClient()
        {
            connector?.Kill();
            connector = null;
            StartClient();
        }
		public static void RefreshDomains()
        {
			if (VanguardImplementation.connector.netcoreStatus != RTCV.NetCore.Enums.NetworkStatus.CONNECTED)
                return;
			PartialSpec gameDone = new PartialSpec("VanguardSpec");
			gameDone[VSPEC.MEMORYDOMAINS_INTERFACES] = GetInterfaces();
			AllSpec.VanguardSpec.Update(gameDone);
			LocalNetCoreRouter.Route(RTCV.NetCore.Endpoints.CorruptCore, RTCV.NetCore.Commands.Remote.EventDomainsUpdated, true, true);
		}
		public static MemoryDomainProxy[] GetInterfaces()
        {
			Console.WriteLine($"getInterfaces()");
			List<MemoryDomainProxy> interfaces = new List<MemoryDomainProxy>();
			MemoryDomainCPUMemory cpumem = new MemoryDomainCPUMemory();
			interfaces.Add(new MemoryDomainProxy(cpumem));
			return interfaces.ToArray();
			
        }
        private static void OnMessageRecieved(object sender, NetCoreEventArgs e)
        {
            var message = e.message;
            var simpleMessage = message as NetCoreSimpleMessage;
            var advancedMessage = message as NetCoreAdvancedMessage;

            ConsoleEx.WriteLine(message.Type);
			switch (message.Type) //Handle received messages here
			{
				case RTCV.NetCore.Commands.Remote.AllSpecSent:
					{
						if (VanguardCore.FirstConnect)
						{
							//SyncObjectSingleton.FormExecute(() => { VanguardCore.LoadDefaultAndShowBizhawkForm(); });
							
							VanguardCore.FirstConnect = false;
						}
						RefreshDomains();
					}
					break;
				case RTCV.NetCore.Commands.Basic.SaveSavestate:
					SyncObjectSingleton.FormExecute(() => { e.setReturnValue(VanguardCore.SaveSavestate(advancedMessage.objectValue as string)); });
					break;

				case RTCV.NetCore.Commands.Basic.LoadSavestate:
					{
						var cmd = advancedMessage.objectValue as object[];
						var path = cmd[0] as string;
						var location = (StashKeySavestateLocation)cmd[1];
						SyncObjectSingleton.FormExecute(() => { e.setReturnValue(VanguardCore.LoadSavestate(path, location)); });
						break;
					}

				case RTCV.NetCore.Commands.Remote.LoadROM:
					{
						//var fileName = advancedMessage.objectValue as string;
						//SyncObjectSingleton.FormExecute(() => { VanguardCore.LoadRom_NET(fileName); });
						
					}
					break;
				case RTCV.NetCore.Commands.Remote.CloseGame:
					{
						//SyncObjectSingleton.FormExecute(() => { Hooks.CLOSE_GAME(true); });
					}
					break;

				case RTCV.NetCore.Commands.Remote.DomainGetDomains:
					RefreshDomains();
					break;

				case RTCV.NetCore.Commands.Remote.DomainRefreshDomains:
					RefreshDomains();
					break;

				case RTCV.NetCore.Commands.Remote.KeySetSyncSettings:
					//SyncObjectSingleton.FormExecute(() => { Hooks.BIZHAWK_GETSET_SYNCSETTINGS = (string)advancedMessage.objectValue; });
					break;

				case RTCV.NetCore.Commands.Remote.KeySetSystemCore:
					{
						;
					}
					break;


				case RTCV.NetCore.Commands.Emulator.GetRealtimeAPI:
					e.setReturnValue(VanguardCore.RTE_API);
					break;


				case RTCV.NetCore.Commands.Remote.EventEmuStarted:
					//if (RTC_StockpileManager.BackupedState == null)
					//S.GET<RTC_Core_Form>().AutoCorrupt = false;


					//Todo
					//RTC_NetcoreImplementation.SendCommandToBizhawk(new RTC_Command("REMOTE_PUSHVMDS) { objectValue = MemoryDomains.VmdPool.Values.Select(it => (it as VirtualMemoryDomain).Proto).ToArray() }, true, true);

					//Thread.Sleep(100);

					//		if (RTC_StockpileManager.BackupedState != null)
					//			S.GET<RTC_MemoryDomains_Form>().RefreshDomainsAndKeepSelected(RTC_StockpileManager.BackupedState.SelectedDomains.ToArray());

					//		if (S.GET<RTC_Core_Form>().cbUseGameProtection.Checked)
					//			RTC_GameProtection.Start();

					break;

				case RTCV.NetCore.Commands.Remote.EventCloseEmulator:
					Environment.Exit(-1);
					break;
			}
		}
    }
}
