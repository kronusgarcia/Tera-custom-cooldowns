﻿using System;
using System.Collections.Generic;
using System.IO;

namespace TCC.Data.Databases
{
    public static class AccountBenefitDatabase
    {
        public static Dictionary<uint, string> Benefits;
        public static void Load()
        {
            Benefits = new Dictionary<uint, string>();
            var f = File.OpenText(Environment.CurrentDirectory + "/resources/data/account-benefit.tsv");
            while (true)
            {
                var line = f.ReadLine();
                if (line == null) return;
                var s = line.Split('\t');
                Benefits.Add(uint.Parse(s[0]), s[1]);
            }
        }
    }
}
