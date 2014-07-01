﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
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

  public class CLangDebuggeeProperty : DebuggeeProperty
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private readonly CLangDebugger m_debugger = null;

    private MiVariable m_gdbVariable = null;

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public CLangDebuggeeProperty (CLangDebugger debugger, CLangDebuggeeStackFrame stackFrame, string expression, MiVariable gdbVariable)
      : this (debugger, stackFrame, expression, gdbVariable, new CLangDebuggeeProperty [] { })
    {
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public CLangDebuggeeProperty (CLangDebugger debugger, CLangDebuggeeStackFrame stackFrame, string expression, MiVariable gdbVariable, CLangDebuggeeProperty [] children)
      : base (debugger.Engine, stackFrame, expression, children)
    {
      if (string.IsNullOrWhiteSpace (expression))
      {
        throw new ArgumentNullException ("expression");
      }

      m_debugger = debugger;

      m_gdbVariable = gdbVariable;

      if (m_gdbVariable == null)
      {
        // 
        // If this expression is not a memory address, memory range, or register - create a GDB/MI variable for polling its value.
        // 

        if (!(m_expression.StartsWith ("*0x") || m_expression.StartsWith ("$")))
        {
          m_gdbVariable = CreateGdbVariable (stackFrame, m_expression, true);
        }
      }

      if (m_gdbVariable != null)
      {
        foreach (KeyValuePair <string, MiVariable> keyValuePair in m_gdbVariable.Children)
        {
          m_children.Add (new CLangDebuggeeProperty (this, keyValuePair.Key, keyValuePair.Value));
        }
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public CLangDebuggeeProperty (CLangDebuggeeProperty parent, string expression, MiVariable gdbVariable)
      : this (parent.m_debugger, parent.m_stackFrame as CLangDebuggeeStackFrame, expression, gdbVariable, new CLangDebuggeeProperty [] { })
    {
      m_parent = parent;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public CLangDebuggeeProperty (CLangDebuggeeProperty parent, string expression, MiVariable gdbVariable, CLangDebuggeeProperty [] children)
      : this (parent.m_debugger, parent.m_stackFrame as CLangDebuggeeStackFrame, expression, gdbVariable, children)
    {
      m_parent = parent;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public override void Dispose ()
    {
      LoggingUtils.PrintFunction ();

      DeleteGdbVariable (m_gdbVariable);

      base.Dispose ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private MiVariable CreateGdbVariable (CLangDebuggeeStackFrame stackFrame, string expression, bool populateChildren)
    {
      IDebugThread2 stackThread;

      uint stackThreadId;

      LoggingUtils.RequireOk (stackFrame.GetThread (out stackThread));

      LoggingUtils.RequireOk (stackThread.GetThreadId (out stackThreadId));

      MiResultRecord resultRecord = m_debugger.GdbClient.SendCommand (string.Format ("-var-create --thread {0} --frame {1} - * {2} ", stackThreadId, stackFrame.StackLevel, StringUtils.Escape (m_expression)));

      if ((resultRecord == null) || ((resultRecord != null) && resultRecord.IsError ()))
      {
        throw new InvalidOperationException ();
      }

      MiVariable variable = new MiVariable (m_expression, resultRecord.Results);

      if (variable.HasChildren)
      {
        resultRecord = m_debugger.GdbClient.SendCommand (string.Format ("-var-list-children --thread {0} --frame {1} --all-values {2}", stackThreadId, stackFrame.StackLevel, variable.Name));

        if ((resultRecord == null) || ((resultRecord != null) && resultRecord.IsError ()))
        {
          throw new InvalidOperationException ();
        }
      }

      MiResultValueList childrenList = resultRecord ["children"] [0] as MiResultValueList;

      for (int i = 0; i < childrenList.Values.Count; ++i)
      {
        MiResultValueTuple childTuple = childrenList [0] as MiResultValueTuple;

        variable.PopulateChild (childTuple ["exp"] [0].GetString (), childTuple.Values);
      }

      return variable;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private void UpdateGdbVariable (MiVariable gdbVariable)
    {
      MiResultRecord resultRecord = m_debugger.GdbClient.SendCommand (string.Format ("-var-update --all-values {0}", gdbVariable.Name));

      if ((resultRecord == null) || ((resultRecord != null) && resultRecord.IsError ()))
      {
        throw new InvalidOperationException ();
      }

      if (!resultRecord.HasField ("changelist"))
      {
        throw new InvalidOperationException ();
      }

      gdbVariable.Populate (resultRecord ["changelist"]);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private void DeleteGdbVariable (MiVariable gdbVariable)
    {
      MiResultRecord resultRecord = m_debugger.GdbClient.SendCommand (string.Format ("-var-delete {0}", gdbVariable.Name));

      if ((resultRecord == null) || ((resultRecord != null) && resultRecord.IsError ()))
      {
        throw new InvalidOperationException ();
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #region IDebugProperty2 Members

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public override int GetMemoryBytes (out IDebugMemoryBytes2 memoryBytes)
    {
      // 
      // Returns the memory bytes for a property value.
      // 

      LoggingUtils.PrintFunction ();

      return m_debugger.NativeProgram.GetMemoryBytes (out memoryBytes);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public override int GetMemoryContext (out IDebugMemoryContext2 memoryContext)
    {
      // 
      // Returns the memory context for a property value.
      // 

      LoggingUtils.PrintFunction ();

      memoryContext = null;

      try
      {
        if ((m_gdbVariable != null) && m_gdbVariable.Value.StartsWith ("0x"))
        {
          // 
          // Note: Sometimes GDB can return 0xADDRESS <SYMBOL>.
          // 

          string [] valueSegments = m_gdbVariable.Value.Split (' ');

          memoryContext = new DebuggeeCodeContext (m_debugger.Engine, null, new DebuggeeAddress (valueSegments [0]));
        }
        else if (m_expression.StartsWith ("0x"))
        {
          memoryContext = new DebuggeeCodeContext (m_debugger.Engine, null, new DebuggeeAddress (m_expression));
        }

        return DebugEngineConstants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        if (memoryContext == null)
        {
          return DebugEngineConstants.S_GETMEMORYCONTEXT_NO_MEMORY_CONTEXT;
        }
        else
        {
          return DebugEngineConstants.E_FAIL;
        }
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public override int GetPropertyInfo (enum_DEBUGPROP_INFO_FLAGS requestedFields, uint radix, uint timeout, IDebugReference2 [] debugReferenceArray, uint argumentCount, DEBUG_PROPERTY_INFO [] propertyInfoArray)
    {
      // 
      // Fills in a DEBUG_PROPERTY_INFO structure that describes a property.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        LoggingUtils.RequireOk (base.GetPropertyInfo (requestedFields, radix, timeout, debugReferenceArray, argumentCount, propertyInfoArray));

        if ((m_gdbVariable != null) && (requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME) != 0)
        {
          if (m_parent != null)
          {
            propertyInfoArray [0].bstrFullName = string.Format ("{0}.{1}", (m_parent as CLangDebuggeeProperty).m_gdbVariable.Expression, m_gdbVariable.Expression);
          }
          else
          {
            propertyInfoArray [0].bstrFullName = m_gdbVariable.Expression;
          }

          propertyInfoArray [0].dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME;
        }

        if ((m_gdbVariable != null) && (requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME) != 0)
        {
          propertyInfoArray [0].bstrName = m_gdbVariable.Expression;

          propertyInfoArray [0].dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME;
        }

        if ((m_gdbVariable != null) && (requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE) != 0)
        {
          propertyInfoArray [0].bstrType = m_gdbVariable.Type;

          propertyInfoArray [0].dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE;
        }

        if ((m_gdbVariable != null) && (requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE) != 0)
        {
          propertyInfoArray [0].bstrValue = m_gdbVariable.Value;

          propertyInfoArray [0].dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE;
        }

        if ((requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB) != 0)
        {
          propertyInfoArray [0].dwAttrib |= (m_expression.StartsWith ("$")) ? enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_STORAGE_REGISTER | enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_DATA : enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_NONE;

          propertyInfoArray [0].dwAttrib |= ((requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND) != 0) ? enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_AUTOEXPANDED : enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_NONE;

          propertyInfoArray [0].dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB;
        }

        if ((requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_STANDARD) != 0)
        {
        }

        if ((requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP) != 0)
        {
          propertyInfoArray [0].pProperty = this;

          propertyInfoArray [0].dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP;
        }

        if ((requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND) != 0)
        {
        }

        if ((requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NOFUNCEVAL) != 0)
        {
          // 
          // Deprecated.
          // 
        }

        if ((requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_RAW) != 0)
        {
        }

        if ((requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_NO_TOSTRING) != 0)
        {
        }

        if ((requestedFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NO_NONPUBLIC_MEMBERS) != 0)
        {
        }

        return DebugEngineConstants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return DebugEngineConstants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public override int GetSize (out uint size)
    {
      // 
      // Returns the size, in bytes, of the property value.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        IDebugThread2 stackThread;

        uint stackThreadId;

        LoggingUtils.RequireOk (m_stackFrame.GetThread (out stackThread));

        LoggingUtils.RequireOk (stackThread.GetThreadId (out stackThreadId));

        MiResultRecord resultRecord = m_debugger.GdbClient.SendCommand (string.Format ("-data-evaluate-expression --thread {0} --frame {1} \"sizeof({2})\"", stackThreadId, (m_stackFrame as CLangDebuggeeStackFrame).StackLevel, m_expression));

        if ((resultRecord == null) || ((resultRecord != null) && (resultRecord.IsError () || (!resultRecord.HasField ("value")))))
        {
          throw new InvalidOperationException ();
        }

        size = resultRecord ["value"] [0].GetUnsignedInt ();

        return DebugEngineConstants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        size = 0;

        return DebugEngineConstants.S_GETSIZE_NO_SIZE;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public override int SetValueAsString (string value, uint radix, uint timeout)
    {
      // 
      // Sets the value of a property from a string.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        MiResultRecord resultRecord = m_debugger.GdbClient.SendCommand (string.Format ("-var-assign {0} {1}", m_gdbVariable.Name, value));

        if ((resultRecord == null) || ((resultRecord != null) && (resultRecord.IsError () || (!resultRecord.HasField ("value")))))
        {
          throw new InvalidOperationException ();
        }

        UpdateGdbVariable (m_gdbVariable);

        return DebugEngineConstants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return DebugEngineConstants.E_FAIL;
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
