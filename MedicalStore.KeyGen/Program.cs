using MedicalStore.Common.Helpers;

// MedicalStore Pro — License Key Generator (VENDOR TOOL — do not ship to clients)
//
// Usage:
//   MedicalStore.KeyGen                -> interactive menu
//   MedicalStore.KeyGen 30             -> generate a 30-day key
//   MedicalStore.KeyGen lifetime       -> generate a non-expiring key
//   MedicalStore.KeyGen verify RYN-...  -> check a key

Console.WriteLine("=====================================================");
Console.WriteLine("  MedicalStore Pro  —  License Key Generator");
Console.WriteLine("  (c) Riyan  •  0309 8480389");
Console.WriteLine("=====================================================\n");

if (args.Length >= 1 && args[0].Equals("verify", StringComparison.OrdinalIgnoreCase))
{
    var k = args.Length >= 2 ? args[1] : "";
    if (LicenseKey.Validate(k, out var exp, out var life))
        Console.WriteLine(life ? "VALID — lifetime key." : $"VALID — expires {exp:dd MMM yyyy}.");
    else
        Console.WriteLine("INVALID key.");
    return;
}

int days;
if (args.Length >= 1)
{
    if (args[0].Equals("lifetime", StringComparison.OrdinalIgnoreCase)) days = 0;
    else if (!int.TryParse(args[0], out days)) { Console.WriteLine("First argument must be a number of days or 'lifetime'."); return; }
}
else
{
    Console.WriteLine("Enter number of days for the license (e.g. 30, 90, 365),");
    Console.Write("or type 0 / 'lifetime' for a non-expiring key: ");
    var input = Console.ReadLine()?.Trim() ?? "";
    if (input.Equals("lifetime", StringComparison.OrdinalIgnoreCase)) days = 0;
    else if (!int.TryParse(input, out days)) { Console.WriteLine("Not a valid number."); return; }
}

var key = LicenseKey.Generate(days);
Console.WriteLine();
Console.WriteLine("  LICENSE KEY:  " + key);
if (days <= 0)
    Console.WriteLine("  Type:         Lifetime (never expires)");
else
    Console.WriteLine($"  Type:         {days} days  (expires {DateTime.UtcNow.Date.AddDays(days):dd MMM yyyy})");
Console.WriteLine("\n  Give this key to the customer. They enter it in the app's");
Console.WriteLine("  Activation window (shown when the trial ends, or Settings).\n");
