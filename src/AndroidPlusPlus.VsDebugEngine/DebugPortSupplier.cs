﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger.Interop;
using AndroidPlusPlus.Common;
using AndroidPlusPlus.VsDebugCommon;

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace AndroidPlusPlus.VsDebugEngine
{

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  [ComVisible(true)]

  [Guid(DebugEngineGuids.guidDebugPortSupplierStringCLSID)]

  [ClassInterface (ClassInterfaceType.None)]

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  public class DebugPortSupplier : IDebugPortSupplier3, IDebugPortSupplierDescription2
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private class DebugPortEnumerator : DebugEnumerator <IDebugPort2, IEnumDebugPorts2>, IEnumDebugPorts2
    {
      public DebugPortEnumerator (List <IDebugPort2> ports)
        : base (ports)
      {
      }

      public DebugPortEnumerator (IDebugPort2 [] ports)
        : base (ports)
      {
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private Dictionary<Guid, IDebugPort2> m_registeredPorts = new Dictionary<Guid, IDebugPort2> ();

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public DebugPortSupplier ()
    {
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    protected int CreatePort (IDebugPortRequest2 portRequest, out IDebugPort2 port)
    {
      try
      {
        AndroidAdb.Refresh ();

        string requestPortName;

        LoggingUtils.RequireOk (portRequest.GetPortName (out requestPortName));

        if (string.IsNullOrWhiteSpace (requestPortName))
        {
          throw new InvalidOperationException ("Invalid/empty port name");
        }

        AndroidDevice device = AndroidAdb.GetConnectedDeviceById (requestPortName);

        if (device == null)
        {
          throw new InvalidOperationException ("Failed to find a device with the name: " + requestPortName);
        }

        port = new DebuggeePort (this, device);

        return Constants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        port = null;

        return Constants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #region IDebugPortSupplier2 Members

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int AddPort (IDebugPortRequest2 pRequest, out IDebugPort2 ppPort)
    {
      // 
      // Attempt to find a port matching the requested name, refreshes and iterates updated ports via EnumPorts.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        string requestPortName;

        LoggingUtils.RequireOk (CanAddPort ());

        LoggingUtils.RequireOk (pRequest.GetPortName (out requestPortName));

        ppPort = null;

        foreach (KeyValuePair<Guid, IDebugPort2> keyPair in m_registeredPorts)
        {
          string portName;

          IDebugPort2 registeredPort = keyPair.Value;

          LoggingUtils.RequireOk (registeredPort.GetPortName (out portName));

          if (portName.Equals (requestPortName))
          {
            ppPort = registeredPort;

            break;
          }
        }

        if (ppPort == null)
        {
          LoggingUtils.RequireOk (CreatePort (pRequest, out ppPort));
        }

        Guid portId;

        LoggingUtils.RequireOk (ppPort.GetPortId (out portId));

        m_registeredPorts.Add (portId, ppPort);

        return Constants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        ppPort = null;

        return Constants.E_PORTSUPPLIER_NO_PORT;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int CanAddPort ()
    {
      // 
      // Verifies that a port supplier can add new ports.
      // 

      LoggingUtils.PrintFunction ();

      return Constants.S_OK;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int EnumPorts (out IEnumDebugPorts2 ppEnum)
    {
      // 
      // Retrieves a list of all the ports supplied by a port supplier.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        IDebugPort2 [] ports = new IDebugPort2 [m_registeredPorts.Count];

        m_registeredPorts.Values.CopyTo (ports, 0);

        ppEnum = new DebugPortEnumerator (ports);

        return Constants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        ppEnum = null;

        return Constants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int GetPort (ref Guid guidPort, out IDebugPort2 ppPort)
    {
      // 
      // Gets a port from a port supplier.
      // 

      LoggingUtils.PrintFunction ();

      ppPort = null;

      try
      {
        if (!m_registeredPorts.TryGetValue (guidPort, out ppPort))
        {
          return Constants.E_PORTSUPPLIER_NO_PORT;
        }

        return Constants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return Constants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int GetPortSupplierId (out Guid pguidPortSupplier)
    {
      // 
      // Gets the port supplier identifier.
      // 

      LoggingUtils.PrintFunction ();

      pguidPortSupplier = this.GetType ().GUID;

      return Constants.S_OK;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int GetPortSupplierName (out string pbstrName)
    {
      // 
      // Gets the port supplier name.
      // 

      LoggingUtils.PrintFunction ();

      pbstrName = "Android++";

      return Constants.S_OK;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int RemovePort (IDebugPort2 pPort)
    {
      // 
      // Removes a port. This method removes the port from the port supplier's internal list of active ports.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        Guid portId;

        LoggingUtils.RequireOk (pPort.GetPortId (out portId));

        m_registeredPorts.Remove (portId);

        return Constants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return Constants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #endregion

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #region IDebugPortSupplier3 Members

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int CanPersistPorts ()
    {
      // 
      // This method determines whether the port supplier can persist ports (by writing them to disk) between invocations of the debugger.
      // 

      LoggingUtils.PrintFunction ();

      return Constants.S_FALSE;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int EnumPersistedPorts (BSTR_ARRAY PortNames, out IEnumDebugPorts2 ppEnum)
    {
      // 
      // This method retrieves an object that allows enumeration of the list of persisted ports.
      // 

      LoggingUtils.PrintFunction ();

      ppEnum = null;

      return Constants.E_FAIL;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #endregion

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #region IDebugPortSupplierDescription2 Members

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int GetDescription (enum_PORT_SUPPLIER_DESCRIPTION_FLAGS [] pdwFlags, out string pbstrText)
    {
      // 
      // Retrieves the description and description metadata for the port supplier.
      // 

      LoggingUtils.PrintFunction ();

      pbstrText = "---";

      return Constants.S_OK;
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
