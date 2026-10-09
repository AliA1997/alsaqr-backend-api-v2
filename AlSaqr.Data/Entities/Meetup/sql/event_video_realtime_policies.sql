-- Realtime authorization for the private channel event_video:{eventId}.
--
-- NOT APPLIED. This file must be reviewed and applied by a human. Nothing in the
-- codebase or tests runs it.
--
-- Clients MUST subscribe with config: { private: true }. The backend publishes with
-- the service-role key (which bypasses RLS) and "private": true, so only the
-- policies below govern what browsers can do on these topics.
--
-- Assumes "alsaqr-2026".attendees.user_id = auth.uid(). Verify before applying.

alter table realtime.messages enable row level security;

-- SELECT: only attendees of the event in the topic may receive broadcasts.
-- Event organizers are attendees too (event_attendees.is_event_organizer).
create policy "event_video_attendees_can_receive"
  on realtime.messages
  for select
  to authenticated
  using (
    realtime.messages.extension = 'broadcast'
    and realtime.topic() like 'event\_video:%' escape '\'
    and exists (
      select 1
      from "alsaqr-2026".event_attendees ea
      join "alsaqr-2026".attendees a on a.id = ea.attendee_id
      where a.user_id = auth.uid()
        and ea.event_id::text = substring(realtime.topic() from 13)
    )
  );

-- Deliberately NO insert policy for topics starting with event_video: so clients
-- cannot send (forge) participant_joined / role_changed / stream_ended. Only the
-- service-role backend publishes on these topics.
