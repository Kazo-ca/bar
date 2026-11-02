# Essai sans ombre extérieure — 1 novembre 2026

Benoit a autorisé l'essai sans ombre. Pour le scénario manuel « un seul clic dans le texte visible à droite du menu ouvert, puis molette : premier clic reçu, menu fermé et défilement effectif », il a répondu **« tout fonctionne comme je le veux »**, selon le retour transmis à cette session par le parent.

Ce retour est enregistré comme **MANUAL_REPORTED_SUCCESS pour ce scénario précis**. Les journaux concordent avec une fermeture du menu, la réception d'un clic dans l'espace entre groupes, puis des molettes et un défilement effectif du témoin. Ils ne permettent pas d'attribuer à eux seuls l'auteur ou l'intention des gestes. Aucune réussite supplémentaire n'est déduite de cette déclaration.

Le blocage lié à l'ombre est écarté pour cette variante qui la supprime et pour le scénario accepté. **Un GO global, un choix d'architecture et l'intégration à l'application métier ne sont pas établis.** Les anciens résultats [avec ombre](RESULTS.md) restent conservés et inchangés.

## Variante et kit

- Hôte WPF isolé sous .NET 10, fenêtre Win32 layered via `AllowsTransparency=true`, rendu HTML avec `WebView2CompositionControl`. Ce n'est pas l'hôte MAUI/Blazor du dépôt.
- SDK .NET 10.0.402, runtime .NET 10.0.13 x64, SDK WebView2 1.0.3179.45 et runtime WebView2 chargé 156.0.4314.50.
- Option nouvelle `-NoShadow` / `--no-shadow` : masque seulement l'élément CSS `#shadow`. Le mode avec ombre demeure disponible par défaut ; aucun masque d'entrée ni suivi du curseur n'est ajouté.
- Cette reprise exécute **A : fenêtre unique 960×500**, avec témoin indépendant. Aucun nouveau résultat expérimental sans ombre n'est revendiqué pour B, la barre avec popup séparé.
- Compilation Release : **0 avertissement, 0 erreur** ; vérification JavaScript des six combinaisons rôle/ombre réussie, selon les vérifications rapportées dans cette session. Le manifeste du kit porte le SHA-256 `Probe.dll` : `73476C1BAE5CDE0EDEA0B84901C957F4F1628FA5564E08ABAE2D12BDB8851757`.

Le kit et les traces sont conservés dans [evidence/2026-11-01-no-shadow](evidence/2026-11-01-no-shadow/README.md). Aucun framework Electron/Tauri n'a été installé ou exécuté pour cette suite.

## Observations vérifiées et portée

Tous les horaires sont UTC. `S` désigne `single-composition-27536.jsonl`, `W` désigne `witness-composition-67304.jsonl` dans le dossier de preuves.

| Cas | Observation | Preuve et limite |
|---|---|---|
| Rendu sans ombre | `noShadow=true`, viewport 960×500, DPR1,5 ; menu 320×260 entièrement contenu. | S6–8, S24. L'absence de l'élément d'ombre est une variante du rendu, pas un contournement du hit-test. |
| Ciblage natif, menu fermé puis ouvert | Les relevés F8 dirigent P1/P2/P3/P4/P5/P7/P8 vers le témoin, ainsi que P6 menu ouvert ; C1/C2 applicables dirigent vers le prototype. **P5 est maintenant l'ancien emplacement de l'ombre, vide.** | S14 et S25. Ce sont des requêtes `WindowFromPoint`, pas des gestes. Le premier relevé S10 ciblait un autre processus en arrière-plan, PID14864 non identifié dans ces traces. |
| Premier clic extérieur et fermeture, retour manuel | Benoit rapporte le scénario conforme. À 03:19:27.199 le prototype se désactive vers le témoin ; fermeture à .204 avec `restoreTrigger=false` ; clic témoin reçu à .210 en `(764,343)`, puis sélection modifiée. | S27–30 et W7–10. Cette coordonnée correspond approximativement à CSS `(389,33 ; 48,67)`, dans l'espace entre groupes. Le menu est déjà fermé au `mouse-down` du témoin : état **ouvert → fermé**, pas menu maintenu ouvert pendant le geste. La confirmation du geste unique vient du retour manuel. |
| Molette après fermeture | En `(761,339)`, le témoin reçoit `+120`, `-120`, `-120` à 03:19:29–30. Les deux gestes négatifs produisent chacun `verticalChange=48`, offsets 48 puis 96. | W11–15. La réception positive est tracée ; aucun déplacement de contenu n'est enregistré après le `+120`. Le menu est fermé durant cette séquence. |
| Autres clics du témoin | Dix paires de clic gauche sont enregistrées dans la plage 03:19:27–56, chacune `clickCount=1` et sélection de longueur zéro. Plusieurs coordonnées sont dans l'espace entre groupes ou sous la barre ; le clic `(778,283)` est hors du rectangle natif déplacé et ne qualifie pas un vide intérieur. | W8–47 ; détail dans le registre et le CSV. Gestes non pilotés par les appels d'automatisation de cette session ; ne pas les convertir en répétitions planifiées de P1–P8. |
| Contrôles visibles | Un clic gauche DOM `counter` est reçu à 03:19:37 ; un clic droit et événement `contextmenu` sur ce même contrôle à 03:19:52. Le bouton `quit` reçoit un clic et ferme normalement le processus à 03:20:41. | S32–35, S207–209, S360–368. Ni la valeur finale du compteur ni la visibilité du contexte Chromium ne sont échantillonnées après ces gestes. |
| Focus après clic extérieur | Fermeture sans demande de restitution du focus ; le témoin est actif. Aucune nouvelle activation du prototype n'est enregistrée avant 03:19:37.315, activation suivie du clic DOM sur `counter`. | S27–35, W7 et W28. Cela ne constitue pas un test de saisie continue ni une qualification générale du focus. |
| Déplacement réel | Après les messages `drag`, 159 puis 138 positions natives sont enregistrées. La fenêtre finit d'abord en `(168,293)-(1608,1043)`, puis en `(1462,273)-(2902,1023)`. | S40–200 et S214–353. Taille conservée 1440×750 physiques ; **toutes les mesures restent à 144 DPI**. Le menu était fermé pendant ces mouvements. |

Les fenêtres se sont donc réellement déplacées dans cette reprise, ce qui n'était pas établi dans la première campagne. Aucune transition d'écran ou d'échelle n'en est déduite.

## Ce qui reste à qualifier

- Molette vers le témoin **avec le menu demeurant ouvert**, clics/doubles-clics/clics droits à tous les points, états actif/inactif et dix répétitions par cellule. Le retour manuel ne couvre que le clic extérieur suivi de molette, avec fermeture du menu.
- Clavier complet, Échap et contexte Chromium, saisie continue sans vol de focus, cycles rapides et fermeture au changement d'application. Aucun événement clavier ou édition de texte n'est enregistré dans cette plage de gestes du témoin.
- Recette équivalente de B sans ombre, contrôles et clavier du popup, déplacement avec menu ouvert et réouverture/ancrage.
- Transitions 100 %/150 %, 125 %/200 %, coordonnées négatives, jonctions et bords, déconnexion/reconnexion. Cette reprise reste à 144 DPI ; les relevés statiques antérieurs ne remplacent pas ces tests.
- Plusieurs applications témoins usuelles, bords anticrénelés, variations de rendu, performances et intégration Blazor.

CUA active sa fenêtre cible avant les entrées et refuse un point appartenant à une autre fenêtre. Ses gestes ciblant le témoin ne prouvent donc pas à eux seuls le premier clic extérieur, le maintien d'un menu ouvert ou la conservation du focus. Le retour manuel est conservé séparément de ces limites d'automatisation et des traces.

## Relancer

```powershell
pwsh ./spikes/window-feasibility/build.ps1
pwsh ./spikes/window-feasibility/run.ps1 -Mode witness
pwsh ./spikes/window-feasibility/run.ps1 -Mode single -NoShadow
# Après fermeture de single, pour une future recette de B :
pwsh ./spikes/window-feasibility/run.ps1 -Mode split -NoShadow
```

Les traces locales vont par défaut dans `%TEMP%\DevToolbar-feasibility`. F8 relève uniquement le ciblage natif et le viewport actuel. Voir [observations.csv](evidence/2026-11-01-no-shadow/observations.csv) pour les distinctions entre observation de routage, entrée effectivement reçue, retour manuel et vérification non exécutée.

**Suite à décider :** architecture à retenir et portée d'une intégration au projet principal. Cette reprise n'autorise ni ne réalise cette intégration ; aucune modification de l'application métier n'est incluse dans ce bilan. La comparaison expérimentale Electron/Tauri reste en attente de décision.
