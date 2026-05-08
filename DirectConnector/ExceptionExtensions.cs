//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Runtime.InteropServices;

namespace DirectConnector;

/// <summary>
/// Extension methods for exception handling.
/// Provides the <see cref="IsFatal"/> guard that was available in earlier SDK versions
/// but made internal in Azure.Connectors.Sdk 0.9.0.
/// </summary>
internal static class ExceptionExtensions
{
    /// <summary>
    /// Determines whether the exception is fatal and should not be caught.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    public static bool IsFatal(this Exception exception)
    {
        return exception is OutOfMemoryException or
               StackOverflowException or
               AccessViolationException or
               SEHException or
               ThreadAbortException;
    }
}
