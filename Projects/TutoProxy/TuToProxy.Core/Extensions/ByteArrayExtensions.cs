namespace TuToProxy.Core.Extensions {
    public static class ByteArrayExtensions {
        public static string ToShortDescriptions(this byte[] bytes) {
            return bytes.Length switch {
                1 => $"{bytes.Length} [{bytes[0]:X2}]",
                2 => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}]",
                3 => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}]",
                4 => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}]",
                5 => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}{bytes[4]:X2}]",
                6 => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}{bytes[4]:X2}{bytes[5]:X2}]",
                >= 7 => $"{bytes.Length} [{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}{bytes[4]:X2}{bytes[5]:X2}{bytes[7]:X2}]",
                _ => $"{bytes.Length}"
            };
        }
    }
}
