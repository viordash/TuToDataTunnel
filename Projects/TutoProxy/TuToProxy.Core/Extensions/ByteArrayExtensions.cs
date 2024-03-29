﻿using System;

namespace TuToProxy.Core.Extensions {
    public static class ByteArrayExtensions {
        public static string ToShortDescriptions(this byte[] bytes, bool showPayload = false) {
            return (bytes.Length, showPayload) switch {
                (1, true) => $"{bytes.Length} [{bytes[0]:X2}]",
                (2, true) => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}]",
                (3, true) => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}]",
                (4, true) => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}]",
                (5, true) => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}{bytes[4]:X2}]",
                (6, true) => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}{bytes[4]:X2}{bytes[5]:X2}]",
                (7, true) => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}{bytes[4]:X2}{bytes[5]:X2}{bytes[6]:X2}]",
                (8, true) => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}{bytes[4]:X2}{bytes[5]:X2}{bytes[6]:X2}{bytes[6]:X2}]",
                ( > 8, true) => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}...{bytes[^4]:X2}{bytes[^3]:X2}{bytes[^2]:X2}{bytes[^1]:X2}]",
                _ => $"{bytes.Length}"
            };
        }

        public static string ToShortDescriptions(this ReadOnlyMemory<byte> bytes, bool showPayload = false) {
            return (bytes.Length, showPayload) switch {
                (1, true) => $"{bytes.Length} [{bytes.Slice(0):X2}]",
                (2, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}]",
                (3, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}]",
                (4, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}]",
                (5, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}{bytes.Slice(4):X2}]",
                (6, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}{bytes.Slice(4):X2}{bytes.Slice(5):X2}]",
                (7, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}{bytes.Slice(4):X2}{bytes.Slice(5):X2}{bytes.Slice(6):X2}]",
                (8, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}{bytes.Slice(4):X2}{bytes.Slice(5):X2}{bytes.Slice(6):X2}{bytes.Slice(6):X2}]",
                ( > 8, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}...{bytes.Slice(bytes.Length - 4):X2}{bytes.Slice(bytes.Length - 3):X2}{bytes.Slice(bytes.Length - 2):X2}{bytes.Slice(bytes.Length - 1):X2}]",
                _ => $"{bytes.Length}"
            };
        }

        public static string ToShortDescriptions(this Memory<byte> bytes, bool showPayload = false) {
            return (bytes.Length, showPayload) switch {
                (1, true) => $"{bytes.Length} [{bytes.Slice(0):X2}]",
                (2, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}]",
                (3, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}]",
                (4, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}]",
                (5, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}{bytes.Slice(4):X2}]",
                (6, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}{bytes.Slice(4):X2}{bytes.Slice(5):X2}]",
                (7, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}{bytes.Slice(4):X2}{bytes.Slice(5):X2}{bytes.Slice(6):X2}]",
                (8, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}{bytes.Slice(4):X2}{bytes.Slice(5):X2}{bytes.Slice(6):X2}{bytes.Slice(6):X2}]",
                ( > 8, true) => $"{bytes.Length} [{bytes.Slice(0):X2}{bytes.Slice(1):X2}{bytes.Slice(2):X2}{bytes.Slice(3):X2}...{bytes.Slice(bytes.Length - 4):X2}{bytes.Slice(bytes.Length - 3):X2}{bytes.Slice(bytes.Length - 2):X2}{bytes.Slice(bytes.Length - 1):X2}]",
                _ => $"{bytes.Length}"
            };
        }
    }
}
