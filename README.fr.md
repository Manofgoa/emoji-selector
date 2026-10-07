[<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English](README.md) · <img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français

# Emoji Selector

Une petite application de bureau Windows, qui démarre vite, s'ouvre par un raccourci clavier global, trouve un émoji par mot-clé et le colle dans l'application où vous écriviez.

Chaque mot employé ici a un seul sens, donné dans le [glossaire](GLOSSARY.fr.md).

## Fonctionnalités

- Un `.exe` qui démarre vite, avec une interface graphique, sans installateur
- **Titre secondaire** : `EmojiSelector.exe --title "Global hotkey"` ouvre une fenêtre intitulée *Emoji Selector — Global hotkey* — dans sa barre de titre, son bouton de la barre des tâches et Alt+Tab — pour distinguer des instances qui tournent côte à côte (Claude Code passe le nom de sa session, voir `CLAUDE.md`). La valeur est l'argument qui suit immédiatement `--title` ; manquante ou vide, elle est ignorée ; donnée deux fois, la dernière l'emporte. Elle n'est jamais mémorisée.

## Prévu

- Un raccourci clavier global qui ouvre la fenêtre depuis n'importe quelle application.
- Une zone de recherche qui trouve les émojis par mot-clé, au fil de la frappe.
- L'émoji choisi collé dans l'application qui était active, la fenêtre de nouveau masquée.
- Les favoris et les émojis récents affichés en premier ; des catégories à parcourir sans rien taper.

## Compilation et exécution

Voir [CONTRIBUTING.md § Build](CONTRIBUTING.md#build) (en anglais).

## Technique

C# / WinForms sur .NET 10, n'utilisant que les composants propres à Windows, aucune bibliothèque tierce.

## Licence

[MIT](LICENSE) © Manofgoa. Contributions : voir [CONTRIBUTING.md](CONTRIBUTING.md) ; problèmes de sécurité : voir [SECURITY.md](SECURITY.md).
