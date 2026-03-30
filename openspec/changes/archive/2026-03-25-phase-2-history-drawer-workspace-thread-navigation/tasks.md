## 1. Backend snapshot shape

- [x] 1.1 Inspect `ChatSnapshotDto`, `ChatService.GetSnapshotAsync`, and `useChatMessages(...)` to confirm the drawer can render from the existing chat snapshot.
- [x] 1.2 If the drawer needs extra data, add only small summary fields to the existing snapshot contract; do not add a separate history endpoint or duplicate thread storage.
- [x] 1.3 Keep the chat snapshot workspace-scoped and aligned with the same persisted thread used by `ChatPage`.

## 2. Frontend history drawer

- [x] 2.1 In `nirmata.frontend/src/app/pages/ChatPage.tsx`, replace the placeholder `History` action with the existing `Drawer` primitive from `src/app/components/ui/drawer.tsx`.
- [x] 2.2 Open the drawer from the active workspace thread returned by `useChatMessages(...)`; do not fetch a second thread source unless backend summary data is required.
- [x] 2.3 Render one read-only row per turn with timestamp, role, gate state, run id, next command, and agent label when helpful.
- [x] 2.4 Add a clear close/return path so the user can jump back to the composer and continue typing immediately.
- [x] 2.5 Preserve the main thread scroll position and current composer contents when the drawer opens and closes.

## 3. Verification

- [x] 3.1 Add or update tests so clicking `History` opens the drawer for the active workspace thread.
- [x] 3.2 Verify the drawer renders the same ordered turn data as the main thread.
- [x] 3.3 Verify closing the drawer leaves scroll position and composer draft state unchanged.
- [x] 3.4 If backend summary fields were added, add API/service coverage proving the snapshot still returns the full thread plus the lightweight metadata.
