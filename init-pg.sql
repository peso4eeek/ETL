CREATE TABLE groups (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    updated_at TIMESTAMP DEFAULT NOW(),
    deleted_at TIMESTAMP NULL
);

CREATE TABLE students (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    group_id INT REFERENCES groups(id),
    updated_at TIMESTAMP DEFAULT NOW(),
    deleted_at TIMESTAMP NULL
);

CREATE TABLE teachers (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    updated_at TIMESTAMP DEFAULT NOW(),
    deleted_at TIMESTAMP NULL
);

CREATE TABLE group_teachers (
    group_id INT REFERENCES groups(id),
    teacher_id INT REFERENCES teachers(id),
    PRIMARY KEY (group_id, teacher_id)
);

INSERT INTO groups (name)
SELECT 'Group-' || i
FROM generate_series(1, 10000) i;


INSERT INTO teachers (name)
SELECT 'Teacher-' || i
FROM generate_series(1, 2000) i;


INSERT INTO students (name, group_id)
SELECT
    'Student-' || i,
    (i % 10000) + 1
FROM generate_series(1, 500000) i;

INSERT INTO group_teachers (group_id, teacher_id)
SELECT g.id, t.id
FROM groups g
         CROSS JOIN LATERAL (
    SELECT id FROM teachers ORDER BY random() LIMIT 3
) t;


CREATE INDEX idx_groups_sync_dates ON groups(updated_at, deleted_at);
CREATE INDEX idx_students_sync_dates ON students(updated_at, deleted_at);
CREATE INDEX idx_teachers_sync_dates ON teachers(updated_at, deleted_at);

CREATE INDEX idx_students_group_id ON students(group_id);
CREATE INDEX idx_group_teachers_teacher_id ON group_teachers(teacher_id);