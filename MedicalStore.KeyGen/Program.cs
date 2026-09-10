using MedicalStore.Common.Helpers;

// MedicalStore Pro — License Key Generator (VENDOR TOOL — do not ship to clients)
//
// Keys are HARDWARE-LOCKED: a key only works on the machine whose Machine ID it was
// generated for. Ask the customer for the "Machine ID" shown in the app's Activation
// window (format MSP-XXXX-XXXX-XXXX-XXXX), then generate a key for it.
//
// Usage:
//   MedicalStore.KeyGen                                  -> interactive
//   MedicalStore.KeyGen MSP-XXXX-XXXX-XXXX-XXXX 30       -> 30-day key for that PC
//   MedicalStore.KeyGen MSP-XXXX-XXXX-XXXX-XXXX lifetime -> non-expiring key for that PC
//   MedicalStore.KeyGen verify MSP-XXXX-... RYN-...      -> check a key against a Machine ID

Console.WriteLine("=====================================================");
Console.WriteLine("  MedicalStore Pro  —  License Key Generator");
Console.WriteLine("  (c) Riyan  •  0309 8480389   [hardware-locked keys]");
Console.WriteLine("=====================================================\n");

if (args.Length >= 1 && args[0].Equals("verify", StringComparison.OrdinalIgnoreCase))
{
    var mid = args.Length >= 2 ? args[1] : "";
    var k = args.Length >= 3 ? args[2] : "";
    if (LicenseKey.Validate(k, mid, out var exp, out var life))
        Console.WriteLine(life ? "VALID for that Machine ID — lifetime key." : $"VALID for that Machine ID — expires {exp:dd MMM yyyy}.");
    else
        Console.WriteLine("INVALID — wrong key, or not issued for that Machine ID.");
    return;
}

string machineId;
int days;

if (args.Length >= 2)
{
    machineId = args[0];
    days = ParseDays(args[1]);
}
else
{
    Console.Write("Customer's Machine ID (MSP-XXXX-XXXX-XXXX-XXXX): ");
    machineId = (Console.ReadLine() ?? "").Trim();
    if (string.IsNullOrWhiteSpace(machineId)) { Console.WriteLine("Machine ID is required."); return; }

    Console.Write("License length in days (e.g. 30, 90, 365) or 'lifetime': ");
    days = ParseDays(Console.ReadLine() ?? "");
}

if (days == int.MinValue) { Console.WriteLine("Not a valid number of days."); return; }

var key = LicenseKey.Generate(machineId, days);
Console.WriteLine();
Console.WriteLine("  MACHINE ID:   " + machineId);
Console.WriteLine("  LICENSE KEY:  " + key);
if (days <= 0)
    Console.WriteLine("  Type:         Lifetime (never expires)");
else
    Console.WriteLine($"  Type:         {days} days  (expires {DateTime.UtcNow.Date.AddDays(days):dd MMM yyyy})");
Console.WriteLine("\n  This key ONLY works on that machine. Give the key to the");
Console.WriteLine("  customer; they enter it in the app (trial-end window or");
Console.WriteLine("  Settings > License).\n");

static int ParseDays(string s)
{
    s = s.Trim();
    if (s.Equals("lifetime", StringComparison.OrdinalIgnoreCase) || s == "0") return 0;
    return int.TryParse(s, out var d) ? d : int.MinValue;
}
