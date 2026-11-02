# Character project dashboard

Local bonus-feature implementation, October 10, 2026. Source builds and pure-code regressions pass. October 11 live acceptance verified dashboard persistence across Companion restart, independent texture archives, snapshot comparison/restore/recovery, native VRoid Undo/Redo, save/reopen pixel persistence, and document-session safeguards. Existing installer artifacts predate this work.

## Create and reopen

In the Companion, choose **Character project**, then **New project**. Select the `.vroid` file the project belongs to and choose a project folder. Setup creates `character-project.json` there and refuses to overwrite an existing manifest. It references the model without modifying or copying it.

Edit **Project name** and **Notes**, then choose **Save notes and name**. Unsaved changes prompt before closing or changing projects. **Open project** reads an existing manifest. The last opened project is remembered in Companion preferences and loaded when you next open the dashboard. Projects can be edited while VRoid is offline.

Models inside the project folder use relative paths; external models retain their full paths. Moving the project together with its contained model and `Assets` folder preserves those paths. Use **Locate / change model** when an external model has moved. Missing models and images remain listed for recovery.

## Store images and linked textures

**Add reference images** copies selected PNG/JPG files into the project's `Assets` folder. **Open selected image** opens the independent copy through the system's image association. These are image files chosen by the user; the dashboard does not decode or render full-size images in memory. Multi-image import reports how many files were added if a later copy fails.

To archive a linked texture:

1. Open the correct model in VRoid and link a layer.
2. Select **Confirm current model session** in the dashboard and verify that the model open in VRoid is the project's associated model.
3. Select the linked layer and choose **Archive selected linked PNG**.

The bridge reports a document session and linked layers, but no model file path. Session association therefore requires explicit user confirmation. It is not persisted across dashboard restarts or model-association changes. Changing the bridge or VRoid document invalidates subsequent archive actions. The archive records source session/layer identity, and the copied PNG remains independent when the live export changes.

Archive files are durable project assets. They do not automatically restore layers in a reopened model. Existing **Texture snapshots** retains its session-scoped history; the dashboard does not migrate that history or silently infer persistent layer associations.

## Limits and verification

Project metadata is limited to 1 MiB; names to 100 characters; notes to 65,536 characters; image assets to 256 entries and 256 MiB total, with 64 MiB per image. File copies stream through a 64 KiB buffer. Metadata is validated and replaced atomically; an ordinary failed asset save removes its new copy. This is not crash-recovery certification or a model backup system.

Regression coverage includes project identity/name/notes persistence, refusal to overwrite existing projects, relative relocation, external model preservation, missing asset recovery, independent image copies, session isolation, traversal rejection, invalid-save preservation, metadata limits and remembered project selection.

Build succeeds; tests pass. NuGet reports NU1900 because package vulnerability data could not be fetched. That data was not verified in this run.

Pending interactive checks:

- [x] Create a project using a disposable model; save notes, close and reopen the Companion.
- [ ] Add multiple images and open a stored image.
- [x] Confirm the correct VRoid session, archive a linked PNG, then change the live PNG and inspect the independent copy.
- [x] Change VRoid documents and confirm archiving is refused until the new session is confirmed.
- [ ] Move a project containing its model and images; reopen and verify paths.
- [ ] Move an external model, use Locate, and confirm stored images/notes remain intact.
- [ ] Exercise unsaved-note prompts, partial batch import and missing-file messages.

Next bonus feature: reference boards and color palettes built on these explicitly associated project assets.
