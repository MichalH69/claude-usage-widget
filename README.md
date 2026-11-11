# Claude Usage Widget

Malý widget na plochu Windows, který ukazuje využití limitů Claude: session (5 h), týden a Fable, v procentech jako `/usage` v Claude Code, a čas do jejich resetu.

Tři vzhledy:

- **Rychloměr** – budíky ve stylu Windows Vista.
- **Akvárko** – rybky plavou tím dál, čím víc je limit vyčerpaný.
- **Silnice** – kolony aut před semaforem, jedno auto za každých 10 %. Při resetu naskočí zelená a kolona odjede.

## Požadavky

- Windows 10 nebo 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Claude Code](https://docs.claude.com/en/docs/claude-code) přihlášený účtem Claude. Widget čte jeho přihlášení z `~/.claude/.credentials.json` a obnovuje ho přes `claude auth status`.

## Instalace

1. Stáhni `ClaudeUsageWidget.exe` z [Releases](../../releases/latest).
2. Ulož ho do složky, kde zůstane natrvalo, například `%LOCALAPPDATA%\ClaudeUsageWidget`.
3. Spusť ho a pravým tlačítkem zapni **Spouštět s Windows**.

Windows SmartScreen může při prvním spuštění varovat, protože exe není podepsané: *Další informace → Přesto spustit*.

## Aktualizace

Widget jednou denně zkontroluje, jestli je na GitHubu novější verze. Když ji najde, v menu se objeví **Aktualizovat na verzi …**. Nová verze se stáhne a nainstaluje jen po kliknutí. Před výměnou se ověří její kontrolní součet SHA-256 z GitHubu.

## Ovládání

- **Klik:** malá / velká verze.
- **Tažení:** přesun, místo si widget pamatuje.
- **Ozubené kolečko:** vzhled a průhlednost.
- **Tlačítko vedle kolečka:** schová widget do oznamovací oblasti. Ikona tam zůstává pořád: levý klik widget ukáže nebo schová, pravý otevře menu.
- **Pravé tlačítko:** obnovit, minimalizovat, vzhled, velká verze, vždy navrchu, spouštění s Windows, aktualizace.

## Soukromí

Přihlašovací token se čte jen z Claude Code a posílá se jen na `api.anthropic.com`. Nikam se neukládá ani nezapisuje do logu. Nastavení, log a poslední načtené hodnoty jsou v `%APPDATA%\ClaudeUsageWidget`. Widget kromě toho kontaktuje jen GitHub kvůli aktualizacím.

## Sestavení

```
dotnet publish ClaudeUsageWidget.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Testovací verze `Demo/ClaudeUsageWidget.Demo.csproj` má ruční posuvníky hodnot a na síť vůbec nesahá.

Adresa, ze které widget čte využití, není oficiálně zdokumentovaná a Anthropic ji může kdykoli změnit.
