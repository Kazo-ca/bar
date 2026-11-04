# Se connecter à GitHub et à Azure DevOps (OAuth)

Les plugins Work Items et CI/CD acceptent un jeton d'accès personnel (PAT) ou une **connexion OAuth**
(« Sign in with GitHub », « Sign in with Microsoft »). La connexion utilise le flux *device code* : la toolbar
affiche un code, vous l'entrez dans le navigateur, et le jeton obtenu va dans le magasin de secrets choisi dans
Paramètres (Gestionnaire d'identification Windows ou fichier chiffré). Il n'est jamais écrit dans `config.json`.

Pour que le bouton soit actif, il faut déclarer **une fois** une application chez chaque fournisseur, puis coller
son identifiant client dans Paramètres → *Sign-in apps*. Un identifiant client est public : aucun secret client
n'est demandé ni enregistré.

## GitHub

1. Sur GitHub : *Settings* → *Developer settings* → *OAuth Apps* → *New OAuth App*.
2. *Application name* : `DevToolbar` ; *Homepage URL* : l'adresse du dépôt ; *Authorization callback URL* :
   la même adresse (elle n'est pas utilisée par le flux *device code*, mais GitHub l'exige).
3. Cocher **Enable Device Flow**, puis *Register application*.
4. Copier le **Client ID** (il commence par `Ov23li…`) dans Paramètres → *Sign-in apps* → *GitHub client ID*.

La toolbar demande la portée `repo`, qui permet de lire les issues et les exécutions GitHub Actions des dépôts
privés. Le jeton d'une OAuth App n'expire pas ; il se révoque depuis *Settings* → *Applications* sur GitHub.

## Azure DevOps (Microsoft Entra ID)

1. Dans le [portail Azure](https://portal.azure.com) : *Microsoft Entra ID* → *App registrations* → *New registration*.
2. *Name* : `DevToolbar` ; *Supported account types* : comptes de votre organisation (ou multi-locataires) ;
   pas d'URI de redirection.
3. *Authentication* → *Advanced settings* → **Allow public client flows** : *Yes*.
4. *API permissions* → *Add a permission* → *Azure DevOps* → *Delegated* → `user_impersonation`.
   Selon votre organisation, un administrateur doit donner son consentement.
5. Copier l'**Application (client) ID** dans Paramètres → *Sign-in apps* → *Azure DevOps client ID*.
6. *Microsoft tenant* : laisser `organizations` (tout compte professionnel), ou mettre l'ID ou le domaine de
   votre locataire (`contoso.onmicrosoft.com`) si l'application n'est déclarée que pour lui.

La toolbar demande `499b84ac-1321-427f-aa17-267ca6975798/.default offline_access` (Azure DevOps + jeton de
renouvellement). Le jeton d'accès dure environ une heure : il reste en mémoire et il est renouvelé tout seul.
Seul le jeton de renouvellement est enregistré. Si Microsoft le refuse (expiré, révoqué, mot de passe changé),
le plugin affiche « The Azure DevOps sign-in has expired. Sign in again in the plugin settings. ».

## Se connecter

1. Ouvrir les réglages du plugin (Work Items ou CI/CD), choisir le fournisseur du dépôt.
2. Cliquer sur **Sign in with GitHub** (ou **Microsoft**). La toolbar affiche l'adresse et le code.
3. Ouvrir l'adresse, entrer le code et accepter. Le champ affiche alors « ✓ Signed in with … ».
4. Le bouton ✕ supprime la connexion du magasin de secrets ; ✎ permet de coller un PAT à la place.
