# Kazo Bar : conventions de nommage

Cette note consigne les choix de nommage du produit et les propositions qui restent à
valider. Elle prépare un futur renommage ; elle ne réalise aucune migration.

## Choix confirmés

| Usage | Forme retenue |
|---|---|
| Nom affiché | **Kazo Bar**, avec un espace et un seul « r » à Bar. |
| Code .NET | **`Kazo.Bar.…`** pour les projets et espaces de noms, par exemple `Kazo.Bar.Core`, `Kazo.Bar.UI` et `Kazo.Bar.Windows`. |
| Site | **`bar.kazo.ca`**. |
| Futur dépôt GitHub | **`Kazo-CA/Bar`** : organisation `Kazo-CA`, dépôt `Bar`. |

Ces formes déclinent la même identité selon leur usage. Le dépôt actuel reste
`werddomain/dev-toolbar` ; `Kazo-CA/Bar` est la destination prévue, sans création,
transfert ni renommage dans cette PR. Le domaine n'est pas configuré par cette note.

## Suggestions à décider

Les propositions ci-dessous ne sont **pas encore approuvées**. Elles suivent le
projet actuel : .NET 10 et Blazor, hôte Windows principal WPF, hôte Web de test et
ancien hôte MAUI gelé (voir le [README](../README.md)).

| Emplacement | Proposition | Point à valider |
|---|---|---|
| Exécutable Windows | `Kazo.Bar.Windows.exe` | Conserver le suffixe de l'hôte, comme l'actuel `DevToolbar.Windows.exe` défini par son [assemblage](../src/Kazo.Bar.Windows/Kazo.Bar.Windows.csproj). |
| Raccourci et entrée du menu Démarrer | `Kazo Bar` | Utiliser le nom affiché ; décider aussi du nom présenté dans la liste des applications installées. |
| Identité de l'application et de l'installateur | `ca.kazo.bar` comme candidat d'identifiant stable | Choisir le format de distribution avant de fixer ses identifiants. L'identité de l'éditeur doit correspondre à la signature réelle ; elle ne se déduit pas de l'URL. Aucun installateur WPF n'est défini dans le dépôt actuel. |
| Configuration et données utilisateur | `%APPDATA%\Kazo\Bar\` | Conserver `config.json`, `data\`, `backups\` et, si le stockage chiffré est choisi, `secrets.json`, comme sous l'actuelle racine `%APPDATA%\DevToolbar\`. |
| Journaux Windows | `%LOCALAPPDATA%\Kazo\Bar\logs\windows-host.log` | Garder le journal propre à l'hôte, actuellement sous `%LOCALAPPDATA%\DevToolbar\logs\`. |
| Éventuels packages .NET | `Kazo.Bar.<Module>`, par exemple `Kazo.Bar.Core` | Décider quels modules distribuer et vérifier la disponibilité des identifiants avant publication. Aucun package n'est publié ou réservé par cette note. |

La configuration WPF actuelle est **un seul `config.json` par utilisateur** ; les
préférences sont intégrées à ce fichier. Les anciens `global-settings.json` et
`.devtoolbar.json` ne justifient donc pas de nouveaux noms de fichiers dans les
projets. Voir les [chemins actuels](../src/Kazo.Bar.Infrastructure/Storage/AppDataPaths.cs)
et le [backlog](Features.md).

## Avant toute mise en œuvre

Une PR distincte devra fixer les suggestions retenues et préparer la compatibilité
des données existantes : configuration, sauvegardes, données des plugins, secrets
chiffrés et références du Gestionnaire d'identification Windows. Elle devra aussi
vérifier la continuité des mises à jour selon le format d'installation choisi ; un
changement de nom affiché ne suffit pas à garantir cette continuité.

Les noms de projets, assemblies, manifests, chemins, configurations et remotes
actifs conservent ici leur valeur actuelle. Cette note n'annonce pas que le site,
le futur dépôt ou les packages sont disponibles.
