﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using EnvDTE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using AndroidPlusPlus.Common;
using AndroidPlusPlus.VsDebugCommon;
using AndroidPlusPlus.VsDebugEngine;

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace AndroidPlusPlus.VsIntegratedPackage
{

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  public delegate int DebuggerEventListenerDelegate (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  interface DebuggerEventListenerInterface
  {
    int OnSessionCreate (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);

    int OnSessionDestroy (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);

    int OnEngineCreate (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);

    int OnProgramCreate (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);

    int OnProgramDestroy (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);

    int OnAttachComplete (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);

    int OnError (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);

    int OnDebuggerConnectionEvent (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);

    int OnDebuggerLogcatEvent (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);
  }

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  [ComVisible (false)]

  public class DebuggerEventListener : DebuggerEventListenerInterface, IVsDebuggerEvents, IDebugEventCallback2
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private readonly DTE m_dteService;

    private readonly IVsDebugger m_debuggerService;

    private readonly IDebuggerConnectionService m_debuggerConnectionService;

    private uint m_debuggerServiceCookie;

    private Dictionary<Guid, DebuggerEventListenerDelegate> m_eventCallbacks;

    private AsyncRedirectProcess m_adbLogcatProcess = null;

    private DeviceLogcatListener m_adbLogcatListener = null;

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public DebuggerEventListener (DTE dteService, IVsDebugger debuggerService, IDebuggerConnectionService debuggerConnectionService)
    {
      m_dteService = dteService;

      m_debuggerService = debuggerService;

      m_debuggerConnectionService = debuggerConnectionService;

      LoggingUtils.RequireOk (m_debuggerService.AdviseDebuggerEvents (this, out m_debuggerServiceCookie));

      LoggingUtils.RequireOk (m_debuggerService.AdviseDebugEventCallback (this));

      // 
      // Register required listener events and paired process function callbacks.
      // 

      m_eventCallbacks = new Dictionary<Guid, DebuggerEventListenerDelegate> ();

      m_eventCallbacks.Add (ComUtils.GuidOf (typeof (DebugEngineEvent.SessionCreate)), OnSessionCreate);

      m_eventCallbacks.Add (ComUtils.GuidOf (typeof (DebugEngineEvent.SessionDestroy)), OnSessionDestroy);

      m_eventCallbacks.Add (ComUtils.GuidOf (typeof (DebugEngineEvent.EngineCreate)), OnEngineCreate);

      m_eventCallbacks.Add (ComUtils.GuidOf (typeof (DebugEngineEvent.ProgramCreate)), OnProgramCreate);

      m_eventCallbacks.Add (ComUtils.GuidOf (typeof (DebugEngineEvent.ProgramDestroy)), OnProgramDestroy);

      m_eventCallbacks.Add (ComUtils.GuidOf (typeof (DebugEngineEvent.AttachComplete)), OnAttachComplete);

      m_eventCallbacks.Add (ComUtils.GuidOf (typeof (DebugEngineEvent.Error)), OnError);

      m_eventCallbacks.Add (ComUtils.GuidOf (typeof (DebugEngineEvent.DebuggerConnectionEvent)), OnDebuggerConnectionEvent);

      m_eventCallbacks.Add (ComUtils.GuidOf (typeof (DebugEngineEvent.DebuggerLogcatEvent)), OnDebuggerLogcatEvent);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private class DeviceLogcatListener : AsyncRedirectProcess.EventListener
    {
      public void ProcessStdout (object sendingProcess, DataReceivedEventArgs args)
      {
        if (!string.IsNullOrWhiteSpace (args.Data))
        {
          DebuggerOutputWindow.WriteLine (VSConstants.OutputWindowPaneGuid.DebugPane_guid, args.Data);
        }
      }

      public void ProcessStderr (object sendingProcess, DataReceivedEventArgs args)
      {
        if (!string.IsNullOrWhiteSpace (args.Data))
        {
          DebuggerOutputWindow.WriteLine (VSConstants.OutputWindowPaneGuid.DebugPane_guid, args.Data);
        }
      }

      public void ProcessExited (object sendingProcess, EventArgs args)
      {
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #region IVsDebuggerEvents Members

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int OnModeChange (DBGMODE dbgmodeNew)
    {
      LoggingUtils.Print ("[DebuggerEventListener] OnModeChange: " + dbgmodeNew.ToString ());

      switch (dbgmodeNew)
      {
        case DBGMODE.DBGMODE_Design:
        case DBGMODE.DBGMODE_Break:
        case DBGMODE.DBGMODE_Run:
        {
          break;
        }
      }

      return VSConstants.S_OK;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #endregion

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #region IDebugEventCallback2 Members

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int Event (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
    {
      try
      {
        DebuggerEventListenerDelegate callback;

        LoggingUtils.Print ("[DebuggerEventListener] Event: " + riidEvent.ToString ());

        int handle = VsDebugCommon.Constants.E_NOTIMPL;

        if (!m_eventCallbacks.TryGetValue (riidEvent, out callback))
        {
          return handle;
        }

        handle = callback (pEngine, pProcess, pProgram, pThread, pEvent, ref riidEvent, dwAttrib);

        if (handle != VsDebugCommon.Constants.E_NOTIMPL)
        {
          LoggingUtils.RequireOk (handle);
        }

        return VSConstants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return VSConstants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #endregion

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #region DebuggerEventListenerInterface Members

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int OnSessionCreate (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
    {
      LoggingUtils.PrintFunction ();

      return VSConstants.E_NOTIMPL;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int OnSessionDestroy (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
    {
      LoggingUtils.PrintFunction ();

      return VSConstants.E_NOTIMPL;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int OnEngineCreate (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
    {
      LoggingUtils.PrintFunction ();

      return VSConstants.E_NOTIMPL;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int OnProgramCreate (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
    {
      LoggingUtils.PrintFunction ();

      try
      {
        /*DebuggeeProgram p = pProgram as DebuggeeProgram;

        bool worked = false;

        if (pProgram is DebuggeeProgram)
        {
          worked = true;
        }

        Type t = pProgram.GetType ();

        IntPtr pUnk = Marshal.GetIUnknownForObject (pProgram);

        Guid guidDebuggeeProgram = ComUtils.GuidOf (typeof (IDebugProgram2));

        IDebugProgram2 debuggeeProgramObj = (IDebugProgram2) Marshal.GetTypedObjectForIUnknown (pUnk, typeof (IDebugProgram2));

        DebuggeeProgram debuggeeProgram = debuggeeProgramObj as DebuggeeProgram;

        debuggeeProgram = (DebuggeeProgram) Marshal.CreateWrapperOfType (debuggeeProgramObj, typeof (IDebugProgram2));
        */

        return VSConstants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return VSConstants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int OnProgramDestroy (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
    {
      LoggingUtils.PrintFunction ();

      try
      {
        if (m_adbLogcatProcess != null)
        {
          m_adbLogcatProcess.Kill ();

          m_adbLogcatProcess.Dispose ();

          m_adbLogcatProcess = null;
        }

        return VSConstants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return VSConstants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int OnAttachComplete (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
    {
      LoggingUtils.PrintFunction ();

      return VSConstants.E_NOTIMPL;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int OnError (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
    {
      LoggingUtils.PrintFunction ();

      try
      {
        DebugEngineEvent.Error errorEvent = pEvent as DebugEngineEvent.Error;

        enum_MESSAGETYPE [] messageType = new enum_MESSAGETYPE [1];

        string errorFormat, errorHelpFileName;

        int errorReason;

        uint errorType, errorHelpId;

        LoggingUtils.RequireOk (errorEvent.GetErrorMessage (messageType, out errorFormat, out errorReason, out errorType, out errorHelpFileName, out errorHelpId));

        LoggingUtils.RequireOk (m_debuggerConnectionService.LaunchDialogUpdate (errorFormat, true));

        return VSConstants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return VSConstants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int OnDebuggerLogcatEvent (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
    {
      LoggingUtils.PrintFunction ();

      try
      {
        DebugEngineEvent.DebuggerLogcatEvent debuggerLogcatEvent = pEvent as DebugEngineEvent.DebuggerLogcatEvent;

        using (SyncRedirectProcess command = AndroidAdb.AdbCommand (debuggerLogcatEvent.HostDevice, "logcat", "-c"))
        {
          command.StartAndWaitForExit ();
        }

        m_adbLogcatProcess = AndroidAdb.AdbCommandAsync (debuggerLogcatEvent.HostDevice, "logcat", "");

        m_adbLogcatListener = new DeviceLogcatListener ();

        if (m_adbLogcatProcess == null)
        {
          throw new InvalidOperationException ("Failed to launch logcat application.");
        }

        if (m_adbLogcatListener == null)
        {
          throw new InvalidOperationException ("Failed to launch logcat listener.");
        }

        m_adbLogcatProcess.Start (m_adbLogcatListener);

        return VSConstants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return VSConstants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int OnDebuggerConnectionEvent (IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
    {
      LoggingUtils.PrintFunction ();

      try
      {
        DebugEngineEvent.DebuggerConnectionEvent debuggerConnectionEvent = pEvent as DebugEngineEvent.DebuggerConnectionEvent;

        switch (debuggerConnectionEvent.Type)
        {
          case DebugEngineEvent.DebuggerConnectionEvent.EventType.ShowDialog:
          {
            LoggingUtils.RequireOk (m_debuggerConnectionService.LaunchDialogShow ());

            break;
          }

          case DebugEngineEvent.DebuggerConnectionEvent.EventType.CloseDialog:
          {
            LoggingUtils.RequireOk (m_debuggerConnectionService.LaunchDialogClose ());

            break;
          }

          case DebugEngineEvent.DebuggerConnectionEvent.EventType.LogStatus:
          case DebugEngineEvent.DebuggerConnectionEvent.EventType.LogError:
          {
            bool isError = (debuggerConnectionEvent.Type == DebugEngineEvent.DebuggerConnectionEvent.EventType.LogError);

            LoggingUtils.RequireOk (m_debuggerConnectionService.LaunchDialogUpdate (debuggerConnectionEvent.Message, isError));

            break;
          }
        }

        return VSConstants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return VSConstants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #endregion

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  }

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
