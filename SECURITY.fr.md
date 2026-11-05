[<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English](SECURITY.md) · <img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français

# Sécurité

Emoji Selector est une application de bureau hors ligne : elle insère les émojis que vous choisissez
dans l'application où vous tapez. Elle ne se connecte au réseau que lorsque vous cliquez sur **Check for
emoji updates…** dans son menu des réglages : elle demande alors au registre de npm
(`registry.npmjs.org`) la dernière version d'`emojibase-data` et, quand vous acceptez la mise à jour,
télécharge ses fichiers depuis jsDelivr (`cdn.jsdelivr.net`). Rien n'est envoyé hormis ces requêtes ;
les fichiers téléchargés sont vérifiés avant de remplacer le dossier `emoji-data` à côté de l'exe.

Vous avez trouvé une vulnérabilité ? Signalez-la en privé, depuis l'onglet **Security** du dépôt →
**Report a vulnerability**, plutôt que dans un ticket public. Seule la dernière version sur `main` est
prise en charge.
