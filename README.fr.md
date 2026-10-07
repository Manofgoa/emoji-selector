[<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English](README.md) · <img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français

# Emoji Selector

Une petite application de bureau Windows, qui démarre vite, s'ouvre par un raccourci clavier global, trouve un émoji par mot-clé et le colle dans l'application où vous écriviez.

Chaque mot employé ici a un seul sens, donné dans le [glossaire](GLOSSARY.fr.md).

## Fonctionnalités

- Un `.exe` qui démarre vite, avec une interface graphique, sans installateur
- **Titre secondaire** : `EmojiSelector.exe --title "Global hotkey"` ouvre une fenêtre intitulée *Emoji Selector — Global hotkey* — dans son bouton de la barre des tâches, Alt+Tab et l'info-bulle de son icône de notification — pour distinguer des instances qui tournent côte à côte (Claude Code passe le nom de sa session, voir `CLAUDE.md`). La valeur est l'argument qui suit immédiatement `--title` ; manquante ou vide, elle est ignorée ; donnée deux fois, la dernière l'emporte. Elle n'est jamais mémorisée.
- **Icône de notification** : tant qu'elle tourne, l'application vit dans la zone de notification, avec pour icône un smiley 😊 en couleur. Un clic gauche affiche la fenêtre, la ramène devant si d'autres fenêtres la recouvrent, ou la masque si elle est déjà devant ; un clic droit ouvre un menu dont **Exit** quitte l'application. La croix de fermeture (✕) de la fenêtre et Alt+F4 la masquent dans la zone de notification : l'application continue de tourner.
- **Fenêtre** : pas de barre de titre, comme le panneau d'émojis de Windows. La bande vide à droite des onglets déplace la fenêtre, ses bords la redimensionnent ; elle n'est jamais réduite ni agrandie. À droite de cette bande, la roue dentée ouvre un menu dont **Open app folder** montre l'exe dans l'Explorateur de fichiers, et la croix (✕) ferme la fenêtre.
- **Raccourci** : tant que l'application tourne, **Win+;** ouvre sa fenêtre juste sous le curseur de texte de l'application où vous écrivez — ou sous le champ qui a le focus, ou au pointeur de la souris quand cette application ne dit pas où est son curseur — et au-dessus quand il n'y a pas la place en dessous. Pressé de nouveau quand la fenêtre est devant, il la masque et vous revenez là où vous écriviez. Il remplace le panneau d'émojis de Windows : quand l'application ne tourne pas, Win+; ouvre le panneau de Windows comme d'habitude, et Win+. l'ouvre toujours. Sur un clavier AZERTY, Win+; est la touche `; .`. Dans une application lancée en administrateur, c'est le panneau de Windows qui s'ouvre.
- **Catégories** : la fenêtre montre tous les émojis, en couleur, dans une seule grille qui défile en continu — une section par catégorie, sous une rangée d'onglets dans l'ordre du panneau Win+; : Smileys & People, Animals & Nature, Food & Drink, Activities, Travel & Places, Objects, Symbols. Un clic sur un onglet saute à sa section ; le défilement fait suivre l'onglet actif. Survoler un émoji affiche son nom. Pas de drapeaux (la police d'émojis de Windows n'en a pas) ni encore de teintes de peau ; un émoji plus récent que la police de Windows s'affiche en carré.
- **Affichage instantané** : les émojis sont dessinés en arrière-plan dès le démarrage de l'application, si bien que la grille ne les attend jamais, premier défilement compris ; un émoji pas encore dessiné s'affiche un instant en carré vert fluo. Ils sont gardés dans un dossier `cache` à côté de l'exe, pour que les lancements suivants les aient tous d'emblée. Supprimer ce dossier est sans risque : ils sont redessinés. Un exe placé dans un dossier où il ne peut pas écrire (par ex. `Program Files`) les dessine simplement à chaque lancement.
- **Insertion** : un clic sur un émoji le tape dans la fenêtre où vous étiez avant, puis la fenêtre se masque dans la zone de notification, dont l'icône montre désormais cet émoji — de nouveau le smiley au lancement suivant. Windows l'empêche dans une application lancée en administrateur.
- **Zone de recherche** : en haut de la fenêtre, vide et prête à la frappe à chaque ouverture de la fenêtre. Taper un mot-clé — le nom d'un émoji ou l'une de ses étiquettes, en anglais ou en français, sans tenir compte de la casse ni des accents — grise les onglets et n'affiche que les émojis trouvés, les plus pertinents d'abord : le mot entier avant son début, avant un mot qui ne fait que le contenir, la plus grande part du mot couverte d'abord, un nom avant une étiquette (`caca` trouve 💩 avant 🥜 *cacahuète*). **Entrée** insère le premier ; **Échap** vide la zone, ou masque la fenêtre si elle est déjà vide. Vider la zone ramène les catégories là où elles étaient.

## Prévu

- Les favoris et les émojis récents affichés en premier.
- Les teintes de peau, les drapeaux, la copie d'un émoji en texte ou en image, la navigation au clavier dans la grille.

## Compilation et exécution

Voir [CONTRIBUTING.md § Build](CONTRIBUTING.md#build) (en anglais).

## Technique

C# / WinForms sur .NET 10, n'utilisant que les composants propres à Windows, aucune bibliothèque tierce. La liste des émojis et leurs mots-clés, en anglais et en français, viennent des données d'[Emojibase](https://emojibase.dev) (MIT), embarquées dans l'exe : voir [CONTRIBUTING.md § Emoji data](CONTRIBUTING.md#emoji-data) (en anglais).

## Licence

[MIT](LICENSE) © Manofgoa. Contributions : voir [CONTRIBUTING.md](CONTRIBUTING.md) ; problèmes de sécurité : voir [SECURITY.md](SECURITY.md).
