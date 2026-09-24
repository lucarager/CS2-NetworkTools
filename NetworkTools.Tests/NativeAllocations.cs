namespace NetworkTools.Tests {
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    using NUnit.Framework;

    /// <summary>
    ///     Gives the test process the native memory calls of the Unity player.
    ///     A native collection allocates through calls the player implements in native code.
    ///     Outside the player they are missing, and any allocation throws.
    ///     Mono lets a process register such calls itself, the .NET Framework does not.
    ///     A test that allocates calls <see cref="Require" /> first, which skips it without Mono.
    /// </summary>
    [SetUpFixture]
    public unsafe class NativeAllocations {
        /// <summary>
        ///     The delegates behind the registered calls, kept from the garbage collector.
        /// </summary>
        private static readonly List<Delegate> s_Calls = new List<Delegate>();

        /// <summary>
        ///     True when native collections can allocate in this process.
        /// </summary>
        private static bool s_Available;

        private delegate IntPtr MallocTracked(long size, int alignment, int allocator, int skip);

        private delegate IntPtr Malloc(long size, int alignment, int allocator);

        private delegate void Free(IntPtr memory, int allocator);

        private delegate void MemSet(IntPtr destination, byte value, long size);

        private delegate void MemCpy(IntPtr destination, IntPtr source, long size);

        private delegate int Count();

        /// <summary>
        ///     Skips the running test when native collections cannot allocate.
        /// </summary>
        public static void Require() {
            if (!s_Available) {
                Assert.Ignore("Native collections allocate outside the game under Mono only.");
            }
        }

        /// <summary>
        ///     Registers the calls once, before any test runs.
        /// </summary>
        [OneTimeSetUp]
        public void Register() {
            if (Type.GetType("Mono.Runtime") == null) {
                return;
            }

            const string memory = "Unity.Collections.LowLevel.Unsafe.UnsafeUtility::";
            const string jobs   = "Unity.Jobs.LowLevel.Unsafe.JobsUtility::";

            Add(memory + "MallocTracked", new MallocTracked(Allocate));
            Add(memory + "Malloc", new Malloc(Allocate));
            Add(memory + "FreeTracked", new Free(Release));
            Add(memory + "Free", new Free(Release));
            Add(memory + "MemSet", new MemSet(Fill));
            Add(memory + "MemCpy", new MemCpy(Copy));
            Add(memory + "MemMove", new MemCpy(Copy));
            Add(jobs + "get_ThreadIndex", new Count(One));
            Add(jobs + "get_ThreadIndexCount", new Count(Many));
            Add(jobs + "get_JobWorkerMaximumCount", new Count(Many));

            s_Available = true;
        }

        [DllImport("__Internal")]
        private static extern void mono_add_internal_call(string name, IntPtr method);

        private static void Add(string name, Delegate call) {
            s_Calls.Add(call);
            mono_add_internal_call(name, Marshal.GetFunctionPointerForDelegate(call));
        }

        private static IntPtr Allocate(long size, int alignment, int allocator, int skip) {
            return Marshal.AllocHGlobal((IntPtr)Math.Max(size, 1));
        }

        private static IntPtr Allocate(long size, int alignment, int allocator) {
            return Marshal.AllocHGlobal((IntPtr)Math.Max(size, 1));
        }

        private static void Release(IntPtr memory, int allocator) {
            Marshal.FreeHGlobal(memory);
        }

        private static void Fill(IntPtr destination, byte value, long size) {
            var bytes = (byte*)destination;

            for (var i = 0L; i < size; i++) {
                bytes[i] = value;
            }
        }

        private static void Copy(IntPtr destination, IntPtr source, long size) {
            Buffer.MemoryCopy((void*)source, (void*)destination, size, size);
        }

        private static int One() {
            return 1;
        }

        private static int Many() {
            return 128;
        }
    }
}
