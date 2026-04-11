import os
import re
import glob

def process_files():
    # Find all .cs and .xaml files
    files = []
    base_dir = r"c:\Users\SCS\Desktop\pos\MedicalStore"
    for ext in ("*.cs", "*.xaml"):
        files.extend(glob.glob(os.path.join(base_dir, "**", ext), recursive=True))

    for path in files:
        if "obj" in path or "bin" in path:
            continue
            
        with open(path, 'r', encoding='utf-8') as f:
            content = f.read()

        original_content = content

        if path.endswith(".cs"):
            # Replace interpolation like $"Rs {expr:N2}" with {expr.FormatRs()}
            # Note: Because the expression could be complex, we just replace :N2 with .Format() or similar, 
            # BUT string interpolation doesn't allow extension method calls inside {expr:N2} if we just replace it directly.
            # Wait, {expr:N2} can be replaced by {(expr).Format()}
            content = re.sub(r'\$"Rs \{([^}]+):N2\}"', r'(\1).FormatRs()', content)
            
            # Cases where it's not starting with "Rs "
            content = re.sub(r'\$"\{([^}]+):N2\}"', r'(\1).Format()', content)
            
            # Simple ToString
            content = content.replace('.ToString("N2")', '.Format()')
            content = content.replace("ToString(\"N2\")", "Format()")
            
            # .ToString("N0") is used for quantities or such? Leave it alone.

        elif path.endswith(".xaml"):
            # XAML bindings
            content = re.sub(r'StringFormat=Rs \\\{0:N2\\\}', r'Converter={StaticResource CurrencyConverter}', content)
            content = re.sub(r"StringFormat='Rs \\?\{0:N2\\?\}'", r"Converter={StaticResource CurrencyConverter}", content)
            content = re.sub(r'StringFormat=N2', r'Converter={StaticResource RawCurrencyConverter}', content)
            
            # Wait, 'Rs \{0:N2\}' inside single quotes
            content = re.sub(r"StringFormat='Rs \{0:N2\}'", r"Converter={StaticResource CurrencyConverter}", content)
            content = re.sub(r'StringFormat="Rs \{0:N2\}"', r'Converter={StaticResource CurrencyConverter}', content)

        if content != original_content:
            with open(path, 'w', encoding='utf-8') as f:
                f.write(content)
            print("Updated:", path)

process_files()
