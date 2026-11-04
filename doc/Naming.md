# Kazo Bar : conventions de nommage

Cette note consigne les choix de nommage du produit et ceux qui restent à valider.
Le produit s'appelait auparavant DevToolbar.

## Choix confirmés

| Usage | Forme retenue |
|---|---|
| Nom affiché | **Kazo Bar**, avec un espace et un seul « r » à Bar. |
| Code .NET | **`Kazo.Bar.…`** pour la solution (`Kazo.Bar.slnx`), les projets et les espaces de noms, par exemple `Kazo.Bar.Core`, `Kazo.Bar.UI` et `Kazo.Bar.Windows`. |
| Exécutable Windows | `Kazo.Bar.Windows.exe`, défini par son [projet](../src/Kazo.Bar.Windows/Kazo.Bar.Windows.csproj). |
| Configuration et données utilisateur | `%APPDATA%\Kazo\Bar\` : `config.json`, `data\`, `backups\` et, si le stockage chiffré est choisi, `secrets.json`. Voir les [chemins](../src/Kazo.Bar.Infrastructure/Storage/AppDataPaths.cs). |
| Journaux Windows | `%LOCALAPPDATA%\Kazo\Bar\logs\windows-host.log`. |
| Secrets du Gestionnaire d'identification Windows | `Kazo.Bar:<ref>`. |
| Objet JavaScript de l'interface | `window.KazoBar`. |
| User-Agent HTTP | `KazoBar/1.0`. |
| Site | **`bar.kazo.ca`** (pas encore configuré). |
| Dépôt GitHub | **`Kazo-ca/bar`** ; l'ancienne adresse `werddomain/dev-toolbar` y redirige. |

## Reprise des données DevToolbar

Au premier démarrage, l'hôte WPF copie `%APPDATA%\DevToolbar\` vers `%APPDATA%\Kazo\Bar\`,
seulement si le nouveau dossier est absent ou vide
([`LegacyAppDataMigration`](../src/Kazo.Bar.Infrastructure/Storage/LegacyAppDataMigration.cs)).
L'ancien dossier n'est jamais supprimé : il reçoit `migrated-to-kazo-bar.txt` et sert de
sauvegarde. Si la copie échoue, l'hôte utilise l'ancien dossier pour cette session et
réessaie au démarrage suivant.

Les secrets `DevToolbar:<ref>` du Gestionnaire d'identification sont réécrits sous
`Kazo.Bar:<ref>`, puis l'ancienne entrée est supprimée ; si les deux existent, la nouvelle
est conservée. L'entropie DPAPI `DevToolbar.Secrets.v1` de `secrets.json` **ne change
pas** : la modifier rendrait les secrets existants illisibles.

L'ancien hôte MAUI, gelé, garde ses chemins `%APPDATA%\DevToolbar` et son identité de
paquet `com.devtoolbar.app`. Les anciens journaux sous `%LOCALAPPDATA%\DevToolbar\logs\`
ne sont pas copiés. Les `spikes/` gardent leurs noms d'origine : ce sont des preuves datées.

## Suggestions à décider

| Emplacement | Proposition | Point à valider |
|---|---|---|
| Raccourci et entrée du menu Démarrer | `Kazo Bar` | Utiliser le nom affiché ; décider aussi du nom présenté dans la liste des applications installées. |
| Identité de l'application et de l'installateur | `ca.kazo.bar` comme candidat d'identifiant stable | Choisir le format de distribution avant de fixer ses identifiants. L'identité de l'éditeur doit correspondre à la signature réelle ; elle ne se déduit pas de l'URL. Aucun installateur WPF n'est défini dans le dépôt. |
| Éventuels packages .NET | `Kazo.Bar.<Module>`, par exemple `Kazo.Bar.Core` | Décider quels modules distribuer et vérifier la disponibilité des identifiants avant publication. Aucun package n'est publié ou réservé. |

La configuration WPF est **un seul `config.json` par utilisateur** ; les préférences sont
intégrées à ce fichier. Les anciens `global-settings.json` et `.devtoolbar.json` ne
justifient donc pas de nouveaux noms de fichiers dans les projets. Voir le
[backlog](Features.md).

Quand un format d'installation sera choisi, il faudra vérifier la continuité des mises à
jour ; un changement de nom affiché ne suffit pas à la garantir.
