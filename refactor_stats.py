import sys

path = r"e:\unity\전자오락원정대\Assets\_Project\Scripts\Gameplay\AutoWeaponSystem.cs"

with open(path, "r", encoding="utf-8") as f:
    lines = f.readlines()

new_lines = []
skip_until = -1

for i, line in enumerate(lines):
    if i < skip_until:
        continue
    
    # Replace GetAttackInterval
    if "public float GetAttackInterval(WeaponRuntime weapon)" in line:
        new_lines.append(line)
        new_lines.append("        {\n")
        new_lines.append("            if (weapon == null) return 1f;\n")
        new_lines.append("            if (weapon.Strategy != null) return weapon.Strategy.GetAttackInterval(weapon, this);\n")
        new_lines.append("            return 1f;\n")
        new_lines.append("        }\n")
        # find end of method
        for j in range(i+1, len(lines)):
            if "        }" in lines[j]:
                skip_until = j + 1
                break
        continue

    # Replace GetWeaponBaseDamage
    if "public float GetWeaponBaseDamage(WeaponRuntime weapon)" in line:
        new_lines.append(line)
        new_lines.append("        {\n")
        new_lines.append("            if (weapon == null) return 0f;\n")
        new_lines.append("            if (weapon.Strategy != null) return weapon.Strategy.GetBaseDamage(weapon, this);\n")
        new_lines.append("            return 0f;\n")
        new_lines.append("        }\n")
        for j in range(i+1, len(lines)):
            if "        }" in lines[j]:
                skip_until = j + 1
                break
        continue

    # Replace GetWeaponRange
    if "public float GetWeaponRange(WeaponRuntime weapon)" in line:
        new_lines.append(line)
        new_lines.append("        {\n")
        new_lines.append("            if (weapon == null) return 0f;\n")
        new_lines.append("            if (weapon.Strategy != null) return weapon.Strategy.GetRange(weapon, this);\n")
        new_lines.append("            return 0f;\n")
        new_lines.append("        }\n")
        for j in range(i+1, len(lines)):
            if "        }" in lines[j]:
                skip_until = j + 1
                break
        continue

    # Replace GetSourceColor
    if "private Color GetSourceColor(WeaponRuntime weapon)" in line:
        new_lines.append(line)
        new_lines.append("        {\n")
        new_lines.append("            if (weapon == null) return Color.white;\n")
        new_lines.append("            if (weapon.Strategy != null) return weapon.Strategy.GetSourceColor(weapon, this);\n")
        new_lines.append("            return Color.white;\n")
        new_lines.append("        }\n")
        for j in range(i+1, len(lines)):
            if "        }" in lines[j]:
                skip_until = j + 1
                break
        continue

    new_lines.append(line)

with open(path, "w", encoding="utf-8") as f:
    f.writelines(new_lines)
