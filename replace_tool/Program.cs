using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ReplaceQuotes
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseDir = @"c:\Users\SCS\Desktop\pos\MedicalStore";
            string[] files = Directory.GetFiles(baseDir, "*.xaml", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                if (file.Contains("obj") || file.Contains("bin")) continue;

                string content = File.ReadAllText(file);
                string original = content;

                // Fix quotes inside markup extensions
                content = Regex.Replace(content, @"\{Binding ([^}]+)Converter=""\{StaticResource CurrencyConverter\}""([^\}]*)\}", "{Binding $1Converter={StaticResource CurrencyConverter}$2}");
                content = Regex.Replace(content, @"\{Binding ([^}]+)Converter=""\{StaticResource RawCurrencyConverter\}""([^\}]*)\}", "{Binding $1Converter={StaticResource RawCurrencyConverter}$2}");

                // Fix the case where it was just `{Binding ..., Converter="{StaticResource ...}"}`
                
                if (content != original)
                {
                    File.WriteAllText(file, content);
                    Console.WriteLine("Fixed Regex Quotes: " + file);
                }
            }
        }
    }
}
