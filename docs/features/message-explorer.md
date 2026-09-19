# Message Explorer

Per-host `/messages` page.
Lists every message the endpoint handles.
Editable payload + send button.

## Enable

```csharp
app.MapMessageExplorer(configuration => configuration.GetValue("Messaging:Explorer:Enabled", false));
```

Predicate over configuration, not an environment name.
"Which environments may send test traffic" is a deployment question; a staging box that is allowed should be able to say so without pretending to be Development.
Returning false maps nothing. No route, no page.

## What it does

- List comes from the handler registry. Only types the endpoint actually handles appear.
- Payload built by reflection: strings empty, numbers zero, enums first name, one element per collection, `DateTimeOffset` and `Guid` filled.
- Payloads are **plausible, not valid**. Editing them is the point.
- Send goes through the real pipeline inside a real transaction. Commands via `SendLocalAsync`, events via `PublishAsync`.

## UI

- Sidebar-less topbar with endpoint name + theme toggle.
- One card per message. Kind badge (Command/Event), handler list, editable JSON textarea, Send button.
- Theme shares localStorage key `ops-theme` with the Operations UI.

## Limits

No per-message-type filter.
Enabling the explorer exposes every handled type.
