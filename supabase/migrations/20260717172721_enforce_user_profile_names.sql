alter table medical.user_profiles
    add constraint ck_user_profiles_first_name_not_blank
        check (btrim(first_name) <> ''),
    add constraint ck_user_profiles_last_name_not_blank
        check (btrim(last_name) <> '');
