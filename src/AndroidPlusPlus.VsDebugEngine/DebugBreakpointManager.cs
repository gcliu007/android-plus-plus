﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Debugger.Interop;

using AndroidPlusPlus.Common;

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace AndroidPlusPlus.VsDebugEngine
{

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  public class DebugBreakpointManager
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private List <DebuggeeBreakpointPending> m_pendingBreakpoints;

    private bool m_requiresRefresh = false;

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public DebugBreakpointManager (DebugEngine engine)
    {
      Engine = engine;

      m_pendingBreakpoints = new List<DebuggeeBreakpointPending> ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public DebugEngine Engine { get; protected set; }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int CreatePendingBreakpoint (IDebugBreakpointRequest2 breakpointRequest, out IDebugPendingBreakpoint2 pendingBreakpoint)
    {
      // 
      // Construct and register new pending breakpoint.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        DebuggeeBreakpointPending breakpoint = null;

        BP_REQUEST_INFO [] requestInfo = new BP_REQUEST_INFO [1];

        LoggingUtils.RequireOk (breakpointRequest.GetRequestInfo (enum_BPREQI_FIELDS.BPREQI_BPLOCATION, requestInfo));

        if ((requestInfo [0].bpLocation.bpLocationType & (uint)enum_BP_LOCATION_TYPE.BPLT_FILE_LINE) != 0)
        {
          // 
          // Query the associated document extension, and create a respective pending breakpoint type.
          // 

          string fileName;

          IDebugDocumentPosition2 documentPostion = (IDebugDocumentPosition2)Marshal.GetObjectForIUnknown (requestInfo [0].bpLocation.unionmember2);

          LoggingUtils.RequireOk (documentPostion.GetFileName (out fileName));

          string fileExtension = Path.GetExtension (fileName).ToLower ();

          switch (fileExtension)
          {
            case ".c":
            case ".cpp":
            case ".h":
            case ".hpp":
            case ".asm":
            {
              breakpoint = new CLangDebuggeeBreakpointPending (Engine.NativeDebugger, this, breakpointRequest);

              break;
            }

            case ".java":
            {
              throw new NotImplementedException ();

              break;
            }

            default:
            {
              breakpoint = new DebuggeeBreakpointPending (this, breakpointRequest);

              break;
            }
          }
        }
        else if (requestInfo [0].bpLocation.bpLocationType == (uint)enum_BP_TYPE.BPT_DATA)
        {
          pendingBreakpoint = null;

          throw new NotImplementedException ();
        }

        lock (m_pendingBreakpoints)
        {
          m_pendingBreakpoints.Add (breakpoint);
        }

        pendingBreakpoint = (IDebugPendingBreakpoint2)breakpoint;

        SetDirty ();

        return DebugEngineConstants.S_OK;
      }
      catch (NotImplementedException e)
      {
        LoggingUtils.HandleException (e);

        pendingBreakpoint = null;

        return DebugEngineConstants.E_NOTIMPL;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        pendingBreakpoint = null;

        return DebugEngineConstants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void ClearBreakpoints ()
    {
      // 
      // Called from the debug engine's Detach method to remove the active (bound) breakpoint instructions.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        lock (m_pendingBreakpoints)
        {
          for (int i = m_pendingBreakpoints.Count - 1; i >= 0; --i)
          {
            m_pendingBreakpoints [i].Delete ();
          }

          m_pendingBreakpoints.Clear ();
        }
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        throw;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void SetDirty ()
    {
      LoggingUtils.PrintFunction ();

      m_requiresRefresh = true;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void RefreshBreakpoints ()
    {
      // 
      // Searches registered pending breakpoints to determine whether their status has changed.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        if (m_requiresRefresh)
        {
          lock (m_pendingBreakpoints)
          {
            foreach (DebuggeeBreakpointPending breakpoint in m_pendingBreakpoints)
            {
              breakpoint.RefreshBoundBreakpoints ();

              breakpoint.RefreshErrorBreakpoints ();
            }
          }

          m_requiresRefresh = false;
        }
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public DebuggeeBreakpointPending FindPendingBreakpoint (uint id)
    {
      try
      {
        DebuggeeBreakpointBound boundBreakpoint = FindBoundBreakpoint (id);

        if (boundBreakpoint != null)
        {
          IDebugPendingBreakpoint2 pendingBreakpoint;

          LoggingUtils.RequireOk (boundBreakpoint.GetPendingBreakpoint (out pendingBreakpoint));

          return pendingBreakpoint as DebuggeeBreakpointPending;
        }

        DebuggeeBreakpointError errorBreakpoint = FindErrorBreakpoint (id);

        if (errorBreakpoint != null)
        {
          IDebugPendingBreakpoint2 pendingBreakpoint;

          LoggingUtils.RequireOk (errorBreakpoint.GetPendingBreakpoint (out pendingBreakpoint));

          return pendingBreakpoint as DebuggeeBreakpointPending;
        }
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);
      }

      return null;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public DebuggeeBreakpointBound FindBoundBreakpoint (uint id)
    {
      // 
      // Search all registered pending breakpoints objects for a bound breakpoint matching the requested id.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        lock (m_pendingBreakpoints)
        {
          foreach (DebuggeeBreakpointPending pending in m_pendingBreakpoints)
          {
            // 
            // Check for matching 'bound' breakpoints.
            // 

            uint numBoundBreakpoints;

            IEnumDebugBoundBreakpoints2 enumeratedBoundBreakpoints;

            LoggingUtils.RequireOk (pending.EnumBoundBreakpoints (out enumeratedBoundBreakpoints));

            LoggingUtils.RequireOk (enumeratedBoundBreakpoints.GetCount (out numBoundBreakpoints));

            if (numBoundBreakpoints > 0)
            {
              DebuggeeBreakpointBound [] boundBreakpoints = new DebuggeeBreakpointBound [numBoundBreakpoints];

              LoggingUtils.RequireOk (enumeratedBoundBreakpoints.Next (numBoundBreakpoints, boundBreakpoints, numBoundBreakpoints));

              for (uint i = 0; i < numBoundBreakpoints; ++i)
              {
                if (boundBreakpoints [i] is CLangDebuggeeBreakpointBound)
                {
                  CLangDebuggeeBreakpointBound bound = boundBreakpoints [i] as CLangDebuggeeBreakpointBound;

                  if (bound.GdbBreakpoint.ID == id)
                  {
                    return bound;
                  }
                }
                else
                {
                  throw new NotImplementedException ("Unrecognised bound breakpoint type");
                }
              }
            }
          }
        }
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);
      }

      return null;
    }


    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
   
    public DebuggeeBreakpointError FindErrorBreakpoint (uint id)
    {
      // 
      // Search all registered pending breakpoints objects for a bound breakpoint matching the requested id.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        lock (m_pendingBreakpoints)
        {
          foreach (DebuggeeBreakpointPending pending in m_pendingBreakpoints)
          {
            // 
            // Check for matching 'error' breakpoints.
            // 

            uint numErrorBreakpoints;

            IEnumDebugErrorBreakpoints2 enumeratedErrorBreakpoints;

            LoggingUtils.RequireOk (pending.EnumErrorBreakpoints (enum_BP_ERROR_TYPE.BPET_ALL, out enumeratedErrorBreakpoints));

            LoggingUtils.RequireOk (enumeratedErrorBreakpoints.GetCount (out numErrorBreakpoints));

            if (numErrorBreakpoints > 0)
            {
              DebuggeeBreakpointError [] errorBreakpoints = new DebuggeeBreakpointError [numErrorBreakpoints];

              LoggingUtils.RequireOk (enumeratedErrorBreakpoints.Next (numErrorBreakpoints, errorBreakpoints, numErrorBreakpoints));

              for (uint i = 0; i < numErrorBreakpoints; ++i)
              {
                if (errorBreakpoints [i] is CLangDebuggeeBreakpointError)
                {
                  CLangDebuggeeBreakpointError error = errorBreakpoints [i] as CLangDebuggeeBreakpointError;

                  if (error.GdbBreakpoint.ID == id)
                  {
                    return error;
                  }
                }
                else
                {
                  throw new NotImplementedException ("Unrecognised error breakpoint type");
                }
              }
            }
          }
        }
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);
      }

      return null;
    }
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
