[<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English](README.md) · <img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français

# Emoji Selector

Une petite application de bureau Windows, qui démarre vite, s'ouvre par un raccourci clavier global, trouve un émoji par mot-clé et le colle dans l'application où vous écriviez.

Chaque mot employé ici a un seul sens, donné dans le [glossaire](GLOSSARY.fr.md).

## Fonctionnalités

- Un `.exe` qui démarre vite, avec une interface graphique, sans installateur
- **Titre secondaire** : `EmojiSelector.exe --title "Global hotkey"` ouvre une fenêtre intitulée *Emoji Selector — Global hotkey* — dans son bouton de la barre des tâches, Alt+Tab et l'info-bulle de son icône de notification — pour distinguer des instances qui tournent côte à côte (Claude Code passe le nom de sa session, voir `CLAUDE.md`). La valeur est l'argument qui suit immédiatement `--title` ; manquante ou vide, elle est ignorée ; donnée deux fois, la dernière l'emporte. Elle n'est jamais mémorisée.
- **Icône de notification** : tant qu'elle tourne, l'application vit dans la zone de notification, avec pour icône un smiley 😊 en couleur. Un clic gauche affiche la fenêtre, la ramène devant si d'autres fenêtres la recouvrent, ou la masque si elle est déjà devant ; un clic droit ouvre un menu dont **Exit** quitte l'application. La croix de fermeture (✕) de la fenêtre et Alt+F4 la masquent dans la zone de notification : l'application continue de tourner.
- **Fenêtre** : pas de barre de titre, comme le panneau d'émojis de Windows. Elle s'ouvre sur 16 émojis de large et 8 lignes de haut, quelle que soit la mise à l'échelle de l'écran. La bande vide à droite des onglets déplace la fenêtre, ses bords la redimensionnent — la taille que vous lui donnez revient au lancement suivant, gardée dans un fichier `settings.json` à côté de l'exe ; elle n'est jamais réduite ni agrandie. À droite de cette bande, la roue dentée ouvre un menu dont **Open app folder** montre l'exe dans l'Explorateur de fichiers, **Reset window size** ramène la fenêtre à sa taille par défaut et **Clear frequently used** remet à zéro les émojis fréquents, et la croix (✕) ferme la fenêtre.
- **Raccourci** : tant que l'application tourne, **Win+;** ouvre sa fenêtre juste sous le curseur de texte de l'application où vous écrivez — ou sous le champ qui a le focus, ou au pointeur de la souris quand cette application ne dit pas où est son curseur — et au-dessus quand il n'y a pas la place en dessous. Pressé de nouveau quand la fenêtre est devant, il la masque et vous revenez là où vous écriviez. Il remplace le panneau d'émojis de Windows : quand l'application ne tourne pas, Win+; ouvre le panneau de Windows comme d'habitude, et Win+. l'ouvre toujours. Sur un clavier AZERTY, Win+; est la touche `; .`. Dans une application lancée en administrateur, c'est le panneau de Windows qui s'ouvre.
- **Catégories** : la fenêtre montre tous les émojis, en couleur, dans une seule grille qui défile en continu — une section par catégorie, sous une rangée d'onglets dans l'ordre du panneau Win+; : Smileys & People, Animals & Nature, Food & Drink, Activities, Travel & Places, Objects, Symbols. Un clic sur un onglet saute à sa section ; le défilement fait suivre l'onglet actif. Survoler un émoji affiche son nom. Pas de drapeaux (la police d'émojis de Windows n'en a pas) ni encore de teintes de peau ; un émoji plus récent que la police de Windows s'affiche en carré.
- **Affichage instantané** : les émojis sont dessinés en arrière-plan dès le démarrage de l'application, si bien que la grille ne les attend jamais, premier défilement compris ; un émoji pas encore dessiné s'affiche un instant en carré vert fluo. Ils sont gardés dans un dossier `cache` à côté de l'exe, pour que les lancements suivants les aient tous d'emblée. Supprimer ce dossier est sans risque : ils sont redessinés. Un exe placé dans un dossier où il ne peut pas écrire (par ex. `Program Files`) les dessine simplement à chaque lancement.
- **Fréquents** : le premier onglet, une étoile, montre les émojis que vous utilisez le plus — les plus utilisés d'abord, le plus récent d'abord à égalité, chacun avec son nombre d'utilisations en dessous. Il tient sur trois lignes, quel que soit le nombre d'émojis qu'elles contiennent à la largeur de la fenêtre, et la fenêtre s'ouvre toujours dessus. Chaque utilisation ajoute 1 au compteur de l'émoji, gardé dans un fichier `usage.json` à côté de l'exe ; **Clear frequently used**, dans le menu de la roue dentée, les remet à zéro après confirmation. Avant toute utilisation, l'onglet affiche *No emoji used yet*.
- **Insertion** : un clic sur un émoji le tape dans la fenêtre où vous étiez avant, puis la fenêtre se masque dans la zone de notification, dont l'icône montre désormais cet émoji — de nouveau le smiley au lancement suivant. Windows l'empêche dans une application lancée en administrateur.
- **Zone de recherche** : en haut de la fenêtre, vide et prête à la frappe à chaque ouverture de la fenêtre. Taper un mot-clé — le nom d'un émoji ou l'une de ses étiquettes, en anglais ou en français, sans tenir compte de la casse ni des accents — grise les onglets et n'affiche que les émojis trouvés, les plus pertinents d'abord : le mot entier avant son début, avant un mot qui ne fait que le contenir, la plus grande part du mot couverte d'abord, un nom avant une étiquette (`caca` trouve 💩 avant 🥜 *cacahuète*). **Entrée** insère le premier ; **Échap** vide la zone, ou masque la fenêtre si elle est déjà vide. Vider la zone ramène les catégories là où elles étaient.
- **Clavier** : un émoji est toujours sélectionné, encadré dans la couleur d'accentuation — le premier à l'ouverture de la fenêtre, le premier résultat pendant une recherche, celui sous la souris quand elle bouge. **Entrée** l'insère. Dans la zone de recherche, **↓** passe dans la grille (on reste dans la zone quand rien n'est trouvé) ; là, les **flèches** déplacent la sélection d'une ligne et d'une catégorie à l'autre, **Début** / **Fin** vont au premier / dernier émoji de la catégorie (**Ctrl+Début** / **Ctrl+Fin** : de la grille), **Page préc.** / **Page suiv.** avancent d'un écran, **Tab** / **Maj+Tab** sautent à la catégorie suivante / précédente. **↑** sur la première ligne, ou la frappe d'une lettre, ramène dans la zone de recherche.

## Prévu

- Les favoris affichés en premier.
- Les teintes de peau, les drapeaux, la copie d'un émoji en texte ou en image.

## Compilation et exécution

Voir [CONTRIBUTING.md § Build](CONTRIBUTING.md#build) (en anglais).

## Technique

C# / WinForms sur .NET 10, n'utilisant que les composants propres à Windows, aucune bibliothèque tierce. La liste des émojis et leurs mots-clés, en anglais et en français, viennent des données d'[Emojibase](https://emojibase.dev) (MIT), embarquées dans l'exe : voir [CONTRIBUTING.md § Emoji data](CONTRIBUTING.md#emoji-data) (en anglais).

## Licence

[MIT](LICENSE) © Manofgoa. Contributions : voir [CONTRIBUTING.md](CONTRIBUTING.md) ; problèmes de sécurité : voir [SECURITY.md](SECURITY.md).
