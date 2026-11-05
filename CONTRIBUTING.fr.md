[<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English](CONTRIBUTING.md) · <img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français

# Contribuer

Merci de votre intérêt pour Emoji Selector. Les tickets et les pull requests sont les bienvenus.

## Compilation

- Nécessite le SDK .NET 10, sur **Windows 10 version 2004 ou ultérieure**.
- Exécuter : `dotnet run --project src/EmojiSelector`
- Publier : `dotnet publish src/EmojiSelector -c Release` →
  `src/EmojiSelector/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/EmojiSelector.exe`,
  un exe unique ReadyToRun dépendant du framework, qui nécessite le .NET 10 Desktop Runtime et
  Windows 10 version 2004 ou ultérieure.

Il n'y a pas de projet de tests : vérifiez une modification à la main, dans l'application lancée.

## Données des émojis

La liste des émojis et leurs mots-clés sont les données d'[Emojibase](https://emojibase.dev),
**version 17.0.0** (MIT, voir [sa LICENSE](src/EmojiSelector/Data/Emojibase/LICENSE)), en deux langues
gardées telles que publiées dans `src/EmojiSelector/Data/Emojibase/` et embarquées dans l'exe :

| Fichier | Source | Donne |
|---|---|---|
| `compact.en.json` | `en/compact.json` | La liste : catégories, ordre, noms, mots-clés anglais |
| `compact.fr.json` | `fr/compact.json` | Les mots-clés français, joints par hexcode |
| `subgroups.json` | Dérivé de `en/data.json`, `en/messages.json` et `fr/messages.json` | Les sous-catégories : les noms des sous-groupes, le sous-groupe de chaque émoji |

`version.txt`, à côté d'eux, tient la version ; `LICENSE` et lui sont embarqués aussi. Au lancement,
`Data/EmojiDataFolder` écrit cette copie embarquée dans `emoji-data\` à côté de l'exe quand ce dossier
manque, est abîmé ou plus ancien, et lit le dossier ; `Data/EmojiCatalog` construit les catégories.
*Check for emoji updates…*, dans l'application, remplace le dossier par une version publiée plus
récente (`Data/EmojiDataUpdate`).

Pour mettre à jour la copie embarquée, remplacez les deux fichiers et `LICENSE` par ceux de la nouvelle
version d'`emojibase-data` (`https://cdn.jsdelivr.net/npm/emojibase-data@<version>/<en|fr>/compact.json`),
puis mettez à jour `version.txt` et la version ci-dessus, et générez de nouveau `subgroups.json` — le
script lit `version.txt` et ne télécharge rien d'autre :

```powershell
pwsh src/EmojiSelector/Data/Emojibase/New-Subgroups.ps1
```

*Check for emoji updates…* dérive le même fichier pour une version plus récente
(`Data/EmojiSubgroups`).

## Caractères spéciaux

La **liste par défaut** des caractères spéciaux — cinq familles, les sous-catégories de l'onglet des
caractères spéciaux, chaque caractère avec ses noms anglais et français et ses alias — est
`src/EmojiSelector/Data/SpecialCharacters.json`, embarqué dans l'exe. Au lancement,
`Data/SpecialCharacters` l'écrit dans `special-characters.json` à côté de l'exe **seulement quand ce
fichier manque** : un fichier déjà là est celui de l'utilisateur et n'est jamais écrasé. Modifier la
liste par défaut atteint donc les nouvelles installations et les utilisateurs qui réinitialisent
l'application, pas les autres.

Le fichier est du JSON en camelCase, UTF-8 sans BOM, les caractères écrits tels quels :
`{ "families": [ { "name", "characters": [ { "character", "name", "frenchName", "aliases" } ] } ] }`.
Le nom anglais est le nom Unicode en minuscules (`copyright sign`), le nom français le nom français
usuel (`symbole copyright`).

## Icône de l'application

L'icône de l'application — celle de l'exe et de la fenêtre — est le *Slightly smiling face* des
[Fluent Emoji](https://github.com/microsoft/fluentui-emoji) de Microsoft, style *Color* (MIT, voir
[sa LICENSE](src/EmojiSelector/AppIcon/LICENSE)), gardé tel que publié dans `src/EmojiSelector/AppIcon/` :

| Fichier | Est |
|---|---|
| `slightly_smiling_face_color.svg` | La source, `assets/Slightly smiling face/Color/` du dépôt Fluent Emoji |
| `app.ico` | Généré depuis le SVG : 16, 20, 24, 32, 40, 48, 64 et 256 px, transparent, un PNG chacun |
| `New-AppIcon.ps1` | Le script qui génère `app.ico` |

`app.ico` est commité : la compilation ne lance pas le script. Après une modification du SVG ou des
tailles, relancez-le — il rend le SVG avec Microsoft Edge en mode headless et ne télécharge rien :

```powershell
pwsh src/EmojiSelector/AppIcon/New-AppIcon.ps1
```

L'icône de notification n'est pas ce fichier : elle est dessinée à l'exécution depuis la police
d'émojis de Windows, et montre l'émoji choisi par l'utilisateur.

## Images de drapeaux

La police d'émojis de Windows n'a pas de drapeaux de pays. Les drapeaux de pays et les drapeaux de
subdivisions (Angleterre, Écosse, pays de Galles) sont dessinés à partir des SVG de
[Twemoji](https://github.com/jdecked/twemoji), **version 17.0.3** — dessins de Twitter, Inc et
d'autres contributeurs, sous licence [CC-BY 4.0](https://creativecommons.org/licenses/by/4.0/deed.fr)
(voir [sa LICENSE-GRAPHICS](src/EmojiSelector/Drawing/Twemoji/LICENSE-GRAPHICS)), gardés tels que
publiés, inchangés, dans `src/EmojiSelector/Drawing/Twemoji/` et embarqués dans l'exe :

| Fichier | Est |
|---|---|
| `<code points>.svg` | Un drapeau, `assets/svg/` du dépôt Twemoji : `1f1eb-1f1f7.svg` pour 🇫🇷 |
| `LICENSE-GRAPHICS` | La licence des dessins de Twemoji |
| `version.txt` | La version de Twemoji |
| `Update-FlagImages.ps1` | Le script qui les télécharge |

Seuls les drapeaux dont les données des émojis embarquées ont besoin et que la police de Windows ne
sait pas dessiner sont là (`Drawing/FlagImages.NeedsImage`) ; les autres drapeaux (🏁, 🏳️‍🌈…) sont ceux
de la police. Les fichiers sont commités : la compilation ne télécharge rien. Après une mise à jour des
données des émojis embarquées (voir *Données des émojis*) ou pour passer à une autre version de
Twemoji, lancez le script — il télécharge depuis jsDelivr, un échec laisse les fichiers tels qu'ils
étaient — puis mettez à jour la version ci-dessus :

```powershell
pwsh src/EmojiSelector/Drawing/Twemoji/Update-FlagImages.ps1 -Version <version>
```

À l'exécution, *Check for emoji updates…* télécharge les images de drapeaux qu'une version plus récente
de Twemoji a modifiées ou ajoutées dans `flag-images\` à côté de l'exe (`Data/FlagImagesUpdate`), lues
par-dessus celles embarquées.

## Avant de modifier le code

- [RULES.md](RULES.md) (en anglais) tient les règles que suit chaque modification. Une modification
  qui enfreint l'une d'elles met la règle à jour dans la même pull request.
- [GLOSSARY.fr.md](GLOSSARY.fr.md) donne à chaque mot son unique sens : employez ses termes dans le
  code, les commentaires et la documentation.
- [`workfiles/`](workfiles) (en anglais) garde la conception de chaque fonctionnalité, l'origine des
  règles.

## Pull requests

- Une fonctionnalité ou une correction par pull request, en anglais : code, commentaires, messages de
  commit.
- Suivez le code qui l'entoure : nommage, densité des commentaires, idiomes.
- Aucune nouvelle dépendance : l'application n'utilise que les composants propres à Windows, voir
  [README § Technique](README.fr.md#technique).
- Une modification visible par l'utilisateur met à jour [README.md](README.md) et
  [README.fr.md](README.fr.md).
