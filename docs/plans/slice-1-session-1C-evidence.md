# Slice 1, session 1C — completion evidence

Date: 2026-09-15. **Slice 1 offline done (M3).** S1-7 remains a work-machine check.

## Entry and offline done checks

- Session-start context ran; root, src, tests and the applicable tools instructions were read.
- Entry verify: exit 0, Status pass, no warnings; project-check **15.810 s**.
- The 1A/1B section 4 deliverables existed before implementation.
- Recorded completion verify: exit 0, Status pass, no warnings; project-check **17.384 s**.
  PowerShell lint checked 30 files, tooling Pester passed 89 tests with none skipped,
  and configuration checked 64 files. The full product gate built without restore,
  ran Core tests under en-US and fr-CA, staged bilingual compiled help, and ran product Pester.
- The section 7 test-source scan returned exactly S1-1, S1-2, S1-3, S1-4, S1-5, S1-6.
- [Fixture catalog](../../tests/Fixtures/README.md):
  every 1C fixture has its source/assumption and V-item.
- [TestCase.Live.ps1](../../tests/Live/TestCase.Live.ps1) is complete and passed lint.
  It was **not run** against ADO.
- Both Get-AdoWorkItem and Get-AdoTestCase help sources exist in en-US and fr-CA.
  Compiled help includes all ten commands and is checked under en-US, fr-CA, fr-FR and fr.
- [Roadmap status](README.md#status) records M3 and this run's project-check duration.

## Delivered scope

Capability detection follows the steps-field/category/non-container rules. Category membership
uses each item's owning project and is stored in the bounded session cache; failed lookup
results never become empty membership or NotAStepContainer. Connect/disconnect clears the cache.

Resolution seeds retrieved, rejected and missing roots, batches distinct references by level
in chunks of 200, and includes shared parameters in the first round. The invocation budget
counts additional attempted IDs, including omissions; already fetched roots do not consume it.
Shared-set row XML in Microsoft.VSTS.TCM.Parameters and the join by same-name column/row index
remain explicitly marked V-03 assumptions.

Expansion uses root-initialized active paths, outline numbers, contiguous sequences and source
revision/provenance. Valid missing IDs retain null metadata. ReferenceCount counts group
occurrences in each root's bounded output. All 16 Slice 1 diagnostic codes are exercised.
The service preserves optional metadata as null, literal parameter tokens, root order,
Complete/Partial status and retrieval timestamps.

Get-AdoTestCase implements only ById, including pipeline values/properties, deduplication,
connection provenance, configured limits and overrides, missing/non-container errors,
one warning per partial case, and Strict errors carrying the case and diagnostics.

Supporting changes add format views and bilingual help, finish the opt-in live script,
and raise package/help/export inventory checks from nine to ten commands.

## Acceptance evidence

| ID | Evidence |
| --- | --- |
| S1-1 | Existing work item reconciliation/chunking and Pester coverage retained and passed |
| S1-2 | Service, detector and parser tests; 3.3.1 numbering; stable unresolved groups; local/shared parameters; Pester binding, partial warnings and Strict |
| S1-3 | Repeated and diamond references expand twice and fetch once; self/direct/indirect cycles include root-to-offending-ID chains |
| S1-4 | Depth precedence; exact row boundary; generated fan-out produces exactly one final Truncated marker and stops globally |
| S1-5 | Existing rich-text, encoding, hostile-content and French tests retained and passed |
| S1-6 | Exactly four POST batches for a root plus three shared levels, and zero GET category calls; seeded roots and first-round shared parameters tested; 450 references use 200/200/50 chunks |
| S1-7 | Deferred: signed installed module, six V-items, and manual comparison against the ADO web UI |

Category requests are counted separately: an absent-steps root causes **two cold GETs**
(one for each required category endpoint), each endpoint once per project/session.
Subsequent roots and invocations in that project cause **zero warm GETs**.

### Literal pseudo-code boundary

The §10.5.2 size check occurs after the node's recursive expansion. A limit of 30 in
the generated fan-out fixture therefore yields **34 ordinary rows plus one Truncated row**:
rows 31–33 are nested groups and row 34 is their leaf. This is intentionally the literal
pseudo-code, as requested, and the test asserts the exact 35-row output. The direct-row
boundary retains the first row over the limit before the marker. Cycles are checked before
depth, and depth before cache state. No normative spec rule was changed.

### Diagnostic coverage

| Code | Coverage |
| --- | --- |
| NotAStepContainer | Category membership and per-input Pester errors |
| EmptySteps | Absent field with category membership; present-null field on arbitrary type |
| MalformedStepsXml | Malformed root/shared documents and DTD rejection through service; French partial warning |
| UnknownStepElement | Unknown root element plus independently malformed local data |
| UnknownStepType | Existing parser fixture through service |
| UnexpectedComprefChildren | Existing child-bearing reference fixture through service |
| InvalidSharedStepReference | Missing/non-numeric ref becomes unexpanded group, null metadata and numbered diagnostic |
| UnresolvedSharedStep | Omitted references retain ID and number; missing seeded root is not fetched again |
| SharedStepHasNoSteps | Existing referenced item without steps produces one Warning, no children, Complete status |
| CircularSharedStepReference | Self, direct and indirect cycles; repeated/diamond branches remain valid |
| MaximumDepthExceeded | Over-depth group retains number and full reference chain |
| ExpansionLimitExceeded | Exact single final marker with no later rows |
| ResolutionLimitExceeded | Budget stops fetching; omitted IDs consume budget and later references remain unexpanded |
| NestedEncodingDecoded | Existing nested-encoding fixture through service |
| MalformedParameterData | Malformed local and shared data retain steps and Warning status |
| UnresolvedSharedParameter | Missing shared set retains its ID and steps, with Warning |

## Spec ledger, authorizations and open items

Updated §25 **V-01, V-03 and V-13** with offline evidence; none is claimed live-confirmed.
Prior V-02, V-05 and V-10 evidence remains valid. No normative revision was needed.

No restore, dependency/package changes, installs, external network, live ADO calls,
Git mutation or sandbox escalation was used. Only fake handlers and the authorized
loopback test server were used for tests. Packaging-script inventory changes support the
new command; dependencies and lock files are unchanged. No scope deviations.

Open: S1-7, live V-01/V-02/V-03/V-05/V-10/V-13, optional oracle (not run), and fluent
French review. Shared parameter wire formats remain provisional pending work-machine results.

## French text for review

One new neutral French resource, exercised under fr-CA:

| Resource key | French value |
| --- | --- |
| PartialTestCase | Le cas de test {0} est partiel : {1} erreur(s), {2} avertissement(s), {3} diagnostic(s) d’information. |

The existing sixteen diagnostic translations are unchanged; see the
[1B review](slice-1-session-1B-evidence.md#french-text-for-review).

All new French help prose is listed below, and the full authored topic is
[Get-AdoTestCase (fr-CA)](../commands/fr-CA/Get-AdoTestCase.md).
Machine names, syntax and example commands remain invariant.

### Synopsis

Récupère les cas de test par identifiant et développe les étapes partagées.

### Description

Regroupe les identifiants, supprime les doublons et renvoie les cas dans l’ordre d’entrée. Les éléments d’étapes partagées peuvent aussi être récupérés directement. Le champ d’étapes et les catégories du projet déterminent la capacité de test. Les identifiants absents produisent des erreurs ObjectNotFound; les autres types produisent des erreurs NotAStepContainer. Les diagnostics d’erreur produisent un objet Partial et un avertissement indiquant le nombre de diagnostics. Strict remplace chaque objet partiel par une erreur non bloquante contenant ses diagnostics. Les jetons de paramètres restent littéraux; les lignes de données et les métadonnées des ensembles partagés sont renvoyées séparément.

### Alias note

Aucun alias.

### Example description

Récupère deux cas et affiche leurs étapes numérotées.

### Parameter Id

Identifiants positifs. Accepte les entiers du pipeline et les objets ayant une propriété Id. Conserve la première occurrence de chaque identifiant.

### Parameter MaximumSharedStepDepth

Profondeur maximale d’imbrication des étapes partagées. Remplace testCases.maximumSharedStepDepth.

### Parameter MaximumExpandedSteps

Nombre maximal de lignes développées. Remplace testCases.maximumExpandedSteps.

### Parameter Strict

Remplace les objets partiels par des erreurs non bloquantes. Les diagnostics figurent dans la cible de l’erreur et dans Exception.Data['Diagnostics'].

### Parameter Connection

Connexion explicite; remplace la connexion active de l’espace d’exécution.

### Parameter CollectionUri

Paramètre de provenance masqué, lié aux objets du pipeline. Une autre collection produit ConnectionMismatch avant toute requête pour cette entrée.

### Common parameters

Prend en charge les paramètres communs, notamment ErrorAction, ErrorVariable, WarningVariable, Verbose et Debug.

### Output description

Métadonnées du cas de test, lignes développées, paramètres, références d’étapes partagées et diagnostics.

### Notes

Les identifiants sont propres à la collection. Les recherches de catégories utilisent le projet de chaque élément. Les limites par défaut proviennent de la configuration : profondeur de 10 et 5 000 lignes. Le budget de résolution par défaut est de 10 000 éléments de travail supplémentaires par invocation. La taille est vérifiée après chaque nœud et son développement récursif; la ligne qui dépasse la limite est conservée, suivie d’un seul marqueur Truncated. Aucune ligne ultérieure n’est produite. Les formats des paramètres partagés restent provisoires en attendant V-03.
