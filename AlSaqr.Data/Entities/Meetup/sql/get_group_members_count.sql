drop function if exists "alsaqr-2026".get_group_members_count(uuid, uuid, varchar);

create or replace function "alsaqr-2026".get_group_members_count(
    p_group_id uuid,
    p_user_id uuid,
    p_search_term varchar default null
)
returns bigint
language plpgsql
as $$
declare
    v_count bigint := 0;
begin
    select count(*)
    into v_count
    from "alsaqr-2026".vw_group_members vgm
    where
        vgm.group_id = p_group_id
        and vgm.user_id <> p_user_id
        and (
            p_search_term is null
            or vgm.username ilike '%' || p_search_term || '%'
        );

    return coalesce(v_count, 0);
end;
$$;
