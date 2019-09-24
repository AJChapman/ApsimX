﻿using System;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using Gdk;
using UserInterface.Presenters;
using UserInterface.Views;
using System.Runtime.InteropServices;
using System.Reflection;

namespace UnitTests.ApsimNG.Utilities
{
    public static class GtkUtilities
    {
        public enum ButtonPressType : uint
        {
            LeftClick = 1,
            MiddleClick = 2,
            RightClick = 3,
        };

        /// <summary>
        /// Sends a left-click event to a widget.
        /// </summary>
        public static void Click(Widget target)
        {
            if (target is Button btn)
            {
                btn.Click();
                return;
            }

            if (target is Label lbl)
            {
                GLib.Signal.Emit(lbl, "activate-link", new object[0]);
                return;
            }

            GLib.Signal.Emit(target, "button-press-event", new object[0]);
            GLib.Signal.Emit(target, "button-release-event", new object[0]);
        }

        /// <summary>
        /// Sends a custom button press (click) event to a widget.
        /// </summary>
        /// <param name="target">Widget which should receive the button press event.</param>
        /// <param name="eventType">Type of event to be sent.</param>
        /// <param name="state">Modifiers for the click - ie control click, shift click, etc.</param>
        /// <param name="button">Type of click - ie left click, middle click or right click.</param>
        /// <param name="x">x-coordinate of the click, relative to the top-left corner of the widget.</param>
        /// <param name="y">y-coordinate of the click, relative to the top-left corner of the widget.</param>
        /// <param name="wait">Iff true, will wait for gtk to process the event.</param>
        public static void Click(Widget target, EventType eventType, ModifierType state, ButtonPressType button, double x = 0, double y = 0, bool wait = true)
        {
            Gdk.Window win = target.GdkWindow;

            int rx, ry;
            win.GetRootOrigin(out rx, out ry);

            var nativeEvent = new NativeEventButtonStruct
            {
                type = eventType,
                send_event = 1,
                window = win.Handle,
                state = (uint)state,
                button = (uint)button,
                x = x,
                y = y,
                axes = IntPtr.Zero,
                device = IntPtr.Zero,
                time = Gtk.Global.CurrentEventTime,
                x_root = x + rx,
                y_root = y + ry
            };

            IntPtr ptr = GLib.Marshaller.StructureToPtrAlloc(nativeEvent);
            try
            {
                EventHelper.Put(new EventButton(ptr));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            if (wait)
                // Clear gtk event loop.
                while (GLib.MainContext.Iteration()) ;
        }

        /// <summary>
        /// Sends a keypress event to a widget.
        /// </summary>
        /// <param name="target">Widget which is the target of the keypress event.</param>
        /// <param name="key">Key to be sent.</param>
        /// <param name="state">Modifier keys separated by plus signs. e.g. "Control + Alt".</param>
        /// <param name="wait">Iff true, will wait for gtk to process the event.</param>
        public static void SendKeyPress(Widget target, char key, string state = null, bool wait = true)
        {
            ModifierType modifier = ParseModifier(state);
            Gdk.Key realKey = ParseKey(key);
            TypeKey(target, realKey, modifier);

            if (wait)
                // Wait for gtk to process the event.
                while (GLib.MainContext.Iteration()) ;
        }

        public static void GetTreeViewCoordinates(Gtk.TreeView tree, int rowIndex, int colIndex, out int x, out int y)
        {
            TreePath path = new TreePath(new int[1] { rowIndex });
            TreeViewColumn column = tree.Columns[colIndex];
            Rectangle rect = tree.GetCellArea(path, column);
            x = rect.X;
            y = rect.Y;
        }

        private static void TypeKey(Widget target, Gdk.Key key, ModifierType modifier)
        {
            SendKeyEvent(target, (uint)key, modifier, EventType.KeyPress);
            SendKeyEvent(target, (uint)key, modifier, EventType.KeyRelease);
        }

        /// <summary>
        /// Used internally - sends a keypress event to a widget.
        /// </summary>
        /// <param name="target">Target widget.</param>
        /// <param name="keyVal">Key value being sent.</param>
        /// <param name="state">Key press state - e.g. ctrl click.</param>
        /// <param name="eventType">Type of event to be sent.</param>
        private static void SendKeyEvent(Widget target, uint keyVal, ModifierType state, EventType eventType)
        {
            Gdk.KeymapKey[] keyms = Gdk.Keymap.Default.GetEntriesForKeyval(keyVal);
            if (keyms.Length == 0)
                throw new Exception("Keyval not found");

            Gdk.Window win = target.GdkWindow;

            var nativeEvent = new NativeEventKeyStruct
            {
                type = eventType,
                send_event = 1,
                window = win.Handle,
                state = (uint)state,
                keyval = keyVal,
                group = (byte)keyms[0].Group,
                hardware_keycode = (ushort)keyms[0].Keycode,
                length = 0,
                time = Gtk.Global.CurrentEventTime
            };

            IntPtr ptr = GLib.Marshaller.StructureToPtrAlloc(nativeEvent);
            try
            {
                Gdk.EventHelper.Put(new Gdk.EventKey(ptr));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// Parses a character to a Gdk.Key.
        /// </summary>
        /// <param name="key">Key value.</param>
        private static Gdk.Key ParseKey(char key)
        {
            if (key == '\n')
                return Gdk.Key.Return;
            else
                return (Gdk.Key)Gdk.Global.UnicodeToKeyval(key);

        }

        /// <summary>
        /// Parses a string of plus sign-delimited modifiers to a gdk ModifierType struct.
        /// </summary>
        /// <param name="state">Plus sign-delimited modifier string. e.g. "Control + alt".</param>
        private static ModifierType ParseModifier(string state)
        {
            if (string.IsNullOrEmpty(state))
                return ModifierType.None;

            string[] modifiers = state.Split('+').Select(m => m.Trim().ToLower()).ToArray();
            ModifierType modifier = ModifierType.None;

            foreach (string mod in modifiers)
            {
                switch (mod)
                {
                    case "shift":
                        modifier |= ModifierType.ShiftMask;
                        break;

                    case "lock":
                        modifier |= Gdk.ModifierType.LockMask;
                        break;

                    case "control":
                        modifier |= Gdk.ModifierType.ControlMask;
                        break;

                    case "mod1":
                        modifier |= Gdk.ModifierType.Mod1Mask;
                        break;

                    case "mod2":
                        modifier |= Gdk.ModifierType.Mod2Mask;
                        break;

                    case "mod3":
                        modifier |= Gdk.ModifierType.Mod3Mask;
                        break;

                    case "mod4":
                        modifier |= Gdk.ModifierType.Mod4Mask;
                        break;

                    case "mod5":
                        modifier |= Gdk.ModifierType.Mod5Mask;
                        break;

                    case "super":
                        modifier |= Gdk.ModifierType.SuperMask;
                        break;

                    case "hyper":
                        modifier |= Gdk.ModifierType.HyperMask;
                        break;

                    case "meta":
                        modifier |= Gdk.ModifierType.MetaMask;
                        break;

                    default:
                        modifier |= Gdk.ModifierType.None;
                        break;
                }
            }

            return modifier;
        }

        // Analysis disable InconsistentNaming
        [StructLayout(LayoutKind.Sequential)]
        struct NativeEventButtonStruct
        {
            public Gdk.EventType type;
            public IntPtr window;
            public sbyte send_event;
            public uint time;
            public double x;
            public double y;
            public IntPtr axes;
            public uint state;
            public uint button;
            public IntPtr device;
            public double x_root;
            public double y_root;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativeEventKeyStruct
        {
            public Gdk.EventType type;
            public IntPtr window;
            public sbyte send_event;
            public uint time;
            public uint state;
            public uint keyval;
            public int length;
            public IntPtr str;
            public ushort hardware_keycode;
            public byte group;
            public uint is_modifier;
        }
        // Analysis restore InconsistentNaming
    }
}
