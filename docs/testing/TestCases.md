# Test Cases

## Summary

This is a source-derived design document, not a report of executed tests. All write operations below require an isolated test database and disposable test records. Do not point these cases at production/shared data. Application startup calls `SeedDb.SeedAsync()`, which calls `EnsureCreatedAsync()` and conditionally inserts seed records; use a startup strategy/database approved for testing before running any case.

- Total test cases: **60**
- Priority: High **33**, Medium **25**, Low **2**
- Cases by test type (multi-label counts; a case may be in more than one group): Functional **33**, Negative **26**, Validation **19**, Authentication **12**, Authorization **18**, API **5**, UI **12**, File Upload **3**, Regression **19**
- Coverage is proposed from repository source. No case has been run.
- Roles used: `Admin`, `Employee`, `Teacher`, `Student`, `Pending`. `Anonymous` is a role created in code, not a synonym for an unauthenticated request. `Staff` is not a source role.
- Test data uses placeholders such as `<unique-email>` and `<valid-id>`; use only records created for the isolated test environment.

## Preconditions and safety

1. Use the local test application and isolated SQL Server test database. Do not use production/shared database.
2. The app's startup seed behavior must be accounted for before launch; this document does not authorize running the app, `EnsureCreated`, migrations, or seed operations.
3. Use a test SMTP mailbox/fake mail sink, reCAPTCHA test strategy and isolated Azure Blob/Azurite account where applicable. Never send real mail or upload files to shared storage for these cases.
4. CRUD/deletion cases may mutate data. Create disposable records and clean up only within the approved test database.
5. Expected access behavior for the explicitly flagged controller areas is unresolved; capture actual behavior but do not label it a confirmed bug until the intended security policy is confirmed.

## Authentication

### TC-AUTH-001

- **Title:** Complete login with valid credentials and OTP
- **Module:** Account
- **Test Type:** Functional, Authentication, UI
- **Priority:** High
- **Role:** Admin (repeat for Employee, Teacher and Student as regression variants)
- **Preconditions:** Isolated test account exists; application is running; test reCAPTCHA and SMTP/OTP capture are available.
- **Test Data:** Valid test email/password; valid OTP captured from test mail sink.
- **Steps:** Open `Account/Login`; submit valid credentials and valid reCAPTCHA; capture OTP; open `Account/VerifyOtp`; submit OTP.
- **Expected Result:** Login advances to OTP verification; valid OTP signs in the user and redirects to the matching role dashboard.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SMTP and Google reCAPTCHA. No production account or real mailbox.

### TC-AUTH-002

- **Title:** Reject invalid password
- **Module:** Account
- **Test Type:** Negative, Authentication
- **Priority:** High
- **Role:** Unauthenticated
- **Preconditions:** Test account exists; test reCAPTCHA can be passed.
- **Test Data:** Valid test email; incorrect password; valid reCAPTCHA test response.
- **Steps:** Submit the login form with the incorrect password.
- **Expected Result:** Login is not completed; an invalid-credentials validation message is shown; OTP verification is not reached.
- **Automation Candidate:** High
- **Dependencies / Notes:** No real email should be sent for a failed password.

### TC-AUTH-003

- **Title:** Validate required login fields and rejected/missing reCAPTCHA
- **Module:** Account
- **Test Type:** Validation, Negative, UI
- **Priority:** Medium
- **Role:** Unauthenticated
- **Preconditions:** Login page is available.
- **Test Data:** Empty email/password; malformed email; missing or rejected reCAPTCHA token.
- **Steps:** Submit the form with required fields empty; then submit otherwise valid fields without a valid reCAPTCHA response.
- **Expected Result:** Required/email validation is shown for invalid model data; failed/missing reCAPTCHA displays the controller's verification error and does not authenticate.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** Google reCAPTCHA test configuration; view model requires email and password, with password minimum length 6.

### TC-AUTH-004

- **Title:** Accept a valid six-character login OTP
- **Module:** Account
- **Test Type:** Functional, Authentication
- **Priority:** High
- **Role:** Any existing role
- **Preconditions:** Credentials have passed and OTP is available in the test mail sink/session TempData.
- **Test Data:** Correct six-character OTP.
- **Steps:** Submit the correct code on `Account/VerifyOtp`.
- **Expected Result:** User is signed in, OTP TempData is removed and the role-based redirect is returned.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SMTP/OTP capture and cookie session. OTP view model requires exactly six characters.

### TC-AUTH-005

- **Title:** Reject incorrect OTP and retain verification state
- **Module:** Account
- **Test Type:** Negative, Authentication, Validation
- **Priority:** High
- **Role:** Unauthenticated pending login
- **Preconditions:** Valid credentials have reached OTP verification.
- **Test Data:** Incorrect six-character code.
- **Steps:** Submit an incorrect OTP; inspect the page and submit the correct OTP in the same valid flow.
- **Expected Result:** Incorrect code displays the invalid-OTP message and does not sign in; valid pending OTP state remains available for retry.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** Isolated SMTP/session. No assumptions about a retry limit; none was identified in the inspected action.

### TC-AUTH-006

- **Title:** Open OTP verification without pending login state
- **Module:** Account
- **Test Type:** Negative, Authentication
- **Priority:** Medium
- **Role:** Unauthenticated
- **Preconditions:** Fresh browser session with no login TempData.
- **Test Data:** None.
- **Steps:** Navigate directly to `Account/VerifyOtp`.
- **Expected Result:** Action redirects to `Account/Login` when no login email is present.
- **Automation Candidate:** High
- **Dependencies / Notes:** Session/TempData behavior.

### TC-AUTH-007

- **Title:** Log out an authenticated user
- **Module:** Account
- **Test Type:** Functional, Authentication, Regression
- **Priority:** High
- **Role:** Any authenticated role
- **Preconditions:** Test user is signed in.
- **Test Data:** None.
- **Steps:** Invoke `Account/Logout`; then open a dashboard requiring authentication.
- **Expected Result:** Sign-out occurs and user is redirected to Home; subsequent protected navigation requires authentication.
- **Automation Candidate:** High
- **Dependencies / Notes:** Disposable test account/session. Source action has no explicit HTTP verb attribute.

### TC-AUTH-008

- **Title:** Register a new account into Pending workflow
- **Module:** Account
- **Test Type:** Functional, Validation, UI
- **Priority:** High
- **Role:** Unauthenticated
- **Preconditions:** Isolated database and test SMTP mailbox are available.
- **Test Data:** Unique valid email, first/last name, address and valid phone value.
- **Steps:** Submit `Account/Register` with valid data; inspect response, test inbox and isolated identity data.
- **Expected Result:** User creation succeeds, role is `Pending`, and confirmation email is sent. Do not assert another role assignment at registration.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server and SMTP. Creates a user; use unique disposable test identity. Registration has no `[Authorize]` in source.

### TC-AUTH-009

- **Title:** Reject duplicate registration and invalid registration fields
- **Module:** Account
- **Test Type:** Negative, Validation
- **Priority:** Medium
- **Role:** Unauthenticated
- **Preconditions:** A test email already exists in isolated database.
- **Test Data:** Existing email; missing first/last name; invalid email/phone; mismatched confirmation.
- **Steps:** Submit duplicate email; separately submit invalid field combinations.
- **Expected Result:** Duplicate address displays the existing-email error; model validation errors prevent normal registration success.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server. Register action removes Password/Confirm model errors before validation; record actual behavior for those fields rather than assuming their annotations block submission.

### TC-AUTH-010

- **Title:** Confirm a registered email with a valid token
- **Module:** Account
- **Test Type:** Functional, Authentication
- **Priority:** High
- **Role:** Unauthenticated
- **Preconditions:** A disposable Pending test user and valid Identity email-confirmation token/link exist.
- **Test Data:** Correct test `userId` and confirmation token.
- **Steps:** Open `Account/ConfirmEmail` using the valid link; continue to first-password page.
- **Expected Result:** Email confirmation succeeds and action redirects to first-password setup.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server and test SMTP/token capture. Source passes a newly generated temporary password to the next action; compare it with the password originally emailed and record any mismatch as a finding, not a pre-confirmed defect.

### TC-AUTH-011

- **Title:** Reject missing, unknown or invalid email-confirmation token
- **Module:** Account
- **Test Type:** Negative, Authentication
- **Priority:** Medium
- **Role:** Unauthenticated
- **Preconditions:** Application is available.
- **Test Data:** Missing `userId`, missing token, unknown user ID, invalid token.
- **Steps:** Exercise each invalid confirmation-link variant.
- **Expected Result:** Missing/unknown user inputs return Not Found; unsuccessful token confirmation renders the Error view.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server for user lookup; use disposable account only.

### TC-AUTH-012

- **Title:** Set the first password after account confirmation
- **Module:** Account
- **Test Type:** Functional, Validation, Regression
- **Priority:** High
- **Role:** Pending user
- **Preconditions:** Disposable user is confirmed and has reached `ChangeFirstPassword`.
- **Test Data:** Temporary password, compliant new password, matching confirmation; then an incorrect temporary password variant.
- **Steps:** Submit correct temporary password and new password; separately retry with wrong temporary password.
- **Expected Result:** Correct flow updates password and returns to Login; wrong temporary password shows an error and does not complete setup.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server. Identity password rules in `Program.cs`; confirm the temporary-password value supplied by the confirmation redirect before execution.

### TC-AUTH-013

- **Title:** Change password with correct and incorrect current password
- **Module:** Account
- **Test Type:** Functional, Negative, Authentication
- **Priority:** High
- **Role:** Authenticated user
- **Preconditions:** Disposable account is signed in.
- **Test Data:** Correct/incorrect old password; new password and matching confirmation.
- **Steps:** Submit a wrong old password; then submit correct old password and valid new password.
- **Expected Result:** Wrong old password does not change credentials; valid submission changes password and redirects to profile flow.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server and valid test login/OTP setup.

### TC-AUTH-014

- **Title:** Recover password for unknown email and reset with invalid/mismatched data
- **Module:** Account
- **Test Type:** Negative, Validation
- **Priority:** Medium
- **Role:** Unauthenticated
- **Preconditions:** Test SMTP sink available.
- **Test Data:** Unknown email; for reset, mismatched confirmation and invalid/expired token.
- **Steps:** Submit unknown email to RecoverPassword; open ResetPassword with invalid token or mismatched passwords.
- **Expected Result:** Unknown email shows the email-not-found error; invalid reset data does not complete password reset.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server, SMTP/token handling. Never send to a real address.

### TC-AUTH-015

- **Title:** Update profile and synchronize role-specific name fields
- **Module:** Account
- **Test Type:** Functional, Validation, Regression
- **Priority:** Medium
- **Role:** Authenticated Student, Teacher or Employee
- **Preconditions:** Disposable account and matching role entity exist.
- **Test Data:** Valid first/last name, address and phone; invalid overlength address/phone variant.
- **Steps:** Submit a valid profile update; inspect account and corresponding Student/Teacher/Employee names; submit invalid field lengths.
- **Expected Result:** Valid profile is saved and role entity names are synchronized; invalid input does not complete a valid update.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server. Action has no explicit `[Authorize]`; intended access policy requires confirmation before treating access denial as expected.

## User Management

### TC-USER-001

- **Title:** Admin views users and assigned roles
- **Module:** User Management
- **Test Type:** Functional, Authorization, UI
- **Priority:** High
- **Role:** Admin; repeat unauthenticated and non-Admin
- **Preconditions:** Test users with different roles exist.
- **Test Data:** Disposable test identities.
- **Steps:** Open `UserManagement/Index1` as Admin, unauthenticated user and Employee.
- **Expected Result:** Admin sees user list and roles; unauthenticated/non-Admin access is challenged/denied by class-level authorization.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server/Identity. HTTP outcome may be redirect or forbid according to cookie middleware.

### TC-USER-002

- **Title:** Change a user's role
- **Module:** User Management
- **Test Type:** Functional, Authorization, Regression
- **Priority:** High
- **Role:** Admin
- **Preconditions:** Disposable test user exists.
- **Test Data:** Test user ID and an existing role among Admin/Employee/Teacher/Student/Pending.
- **Steps:** Open Edit; select target role; submit; reload user list and inspect role.
- **Expected Result:** Previous roles are replaced with the selected role and action redirects to Index1.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server/Identity. Use only disposable user; Admin-only controller.

### TC-USER-003

- **Title:** Lock and unlock a disposable account
- **Module:** User Management
- **Test Type:** Functional, Authorization
- **Priority:** Medium
- **Role:** Admin
- **Preconditions:** Disposable non-admin user exists and test email sink is configured.
- **Test Data:** Test user ID.
- **Steps:** Invoke ToggleStatus twice as Admin; inspect lockout value, login behavior and test mailbox.
- **Expected Result:** First operation sets a future lockout value and sends suspension mail; second clears lockout and sends reactivation mail. Verify sign-in behavior against the intended lockout policy rather than assuming the stored value alone blocks login.
- **Automation Candidate:** Low
- **Dependencies / Notes:** SQL Server and SMTP. ToggleStatus updates state through an action with no explicit HTTP verb attribute; security policy/method protection requires confirmation.

### TC-USER-004

- **Title:** Reject incorrect deletion OTP without deleting user
- **Module:** User Management
- **Test Type:** Negative, Authentication, Authorization
- **Priority:** High
- **Role:** Admin
- **Preconditions:** Disposable user exists; test SMTP sink configured.
- **Test Data:** Incorrect OTP and disposable target user ID.
- **Steps:** Start Delete flow; submit incorrect OTP to ConfirmDelete; verify target still exists.
- **Expected Result:** Invalid OTP error is shown and user/related test records remain.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server, SMTP and TempData. Use no non-test user.

### TC-USER-005

- **Title:** Delete a disposable user after correct OTP
- **Module:** User Management
- **Test Type:** Functional, Authorization, Regression
- **Priority:** High
- **Role:** Admin
- **Preconditions:** Disposable user and any linked disposable Student/Teacher/Employee records exist; test SMTP sink configured.
- **Test Data:** Target test user and correct deletion OTP.
- **Steps:** Start Delete flow, capture OTP, submit correct OTP, inspect only the isolated database.
- **Expected Result:** Related Student/Teacher/Employee records are removed before user deletion; success message is set and user returns to Index1.
- **Automation Candidate:** Low
- **Dependencies / Notes:** Destructive: isolated SQL Server only, disposable records and test SMTP. Never run on shared/production data.

## Student Management

### TC-STU-001

- **Title:** List students and open existing student details
- **Module:** Student Management
- **Test Type:** Functional, UI, Authorization
- **Priority:** High
- **Role:** Employee or Admin
- **Preconditions:** Test student and related user/class exist.
- **Test Data:** Valid test student ID.
- **Steps:** Open Index; open Details for the test student.
- **Expected Result:** List and detail page show student data and related class/user information.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server; controller requires Employee/Admin.

### TC-STU-002

- **Title:** Create student linked to Pending user and class
- **Module:** Student Management
- **Test Type:** Functional, Validation, Regression
- **Priority:** High
- **Role:** Employee or Admin
- **Preconditions:** Disposable Pending identity and class exist.
- **Test Data:** Valid names, user ID, class ID and optional test image.
- **Steps:** Submit Create with selected Pending user/class; inspect student and identity roles.
- **Expected Result:** Student is saved with class/user relationships; Pending role is replaced with Student.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server. Image storage dependency only if image supplied; requires dedicated test blob storage.

### TC-STU-003

- **Title:** Reject invalid student creation and edit relationship
- **Module:** Student Management
- **Test Type:** Validation, Negative
- **Priority:** High
- **Role:** Employee or Admin
- **Preconditions:** Test form accessible; disposable student/class records available.
- **Test Data:** Missing required first/last name or class; edit with valid alternate class ID.
- **Steps:** Submit create with required data missing; then edit student class and reload details.
- **Expected Result:** Invalid model returns form with validation errors; valid edit persists selected class relationship.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server. StudentViewModel requires first name, last name and SchoolClassId.

### TC-STU-004

- **Title:** Handle missing student ID and delete disposable student
- **Module:** Student Management
- **Test Type:** Negative, Functional
- **Priority:** Medium
- **Role:** Employee or Admin
- **Preconditions:** One disposable student exists; one non-existing ID is established in isolated test DB.
- **Test Data:** Existing disposable student ID; missing ID.
- **Steps:** Request Details/Edit/Delete for missing ID; delete disposable student using confirmation POST; inspect test DB.
- **Expected Result:** Missing record follows controller's not-found path; confirmed deletion removes the disposable student and redirects to Index.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server; destructive only for disposable test row.

### TC-STU-005

- **Title:** Restrict student management to Employee/Admin roles
- **Module:** Student Management
- **Test Type:** Authorization, Regression
- **Priority:** High
- **Role:** Unauthenticated, Teacher, Student; compare Employee/Admin
- **Preconditions:** App available; test accounts for the listed roles exist.
- **Test Data:** Index URL and a read-only student detail URL.
- **Steps:** Request Index/Details as each role and unauthenticated user.
- **Expected Result:** Employee/Admin can access; other users are challenged/denied by controller authorization.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server for successful pages; no writes.

## Teacher Management

### TC-TEACH-001

- **Title:** Complete teacher create, details, edit and delete lifecycle
- **Module:** Teacher Management
- **Test Type:** Functional, UI, Regression
- **Priority:** Medium
- **Role:** Employee or Admin
- **Preconditions:** Disposable Pending user, class and subject exist.
- **Test Data:** Valid teacher fields; selected subject/class; optional test image.
- **Steps:** Create teacher; open details; edit fields/associations; delete disposable teacher.
- **Expected Result:** CRUD lifecycle succeeds; create replaces Pending role with Teacher; edit persists selected subjects/classes.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server. Blob dependency if image supplied. Delete only test records.

### TC-TEACH-002

- **Title:** Validate teacher fields and missing identifiers
- **Module:** Teacher Management
- **Test Type:** Validation, Negative
- **Priority:** Medium
- **Role:** Employee or Admin
- **Preconditions:** Teacher form available.
- **Test Data:** Missing required user/name; missing or non-existing teacher ID.
- **Steps:** Submit invalid Create/Edit data; request Details/Edit/Delete for missing ID.
- **Expected Result:** Model errors are returned for required fields; absent entity follows not-found response/view path.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server. TeacherViewModel requires UserId, FirstName and LastName.

### TC-TEACH-003

- **Title:** Enforce Employee/Admin access to teacher management
- **Module:** Teacher Management
- **Test Type:** Authorization
- **Priority:** High
- **Role:** Unauthenticated, Teacher, Student; compare Employee/Admin
- **Preconditions:** Test role accounts exist.
- **Test Data:** Teacher Index URL.
- **Steps:** Request Index with each identity and without authentication.
- **Expected Result:** Employee/Admin may access; other roles/unauthenticated requests are challenged/denied.
- **Automation Candidate:** High
- **Dependencies / Notes:** Class-level `[Authorize(Roles="Employee,Admin")]`.

## Employee Management

### TC-EMP-001

- **Title:** Complete employee create, details, edit and delete lifecycle
- **Module:** Employee Management
- **Test Type:** Functional, UI, Regression
- **Priority:** Medium
- **Role:** Admin
- **Preconditions:** Disposable Pending identity exists.
- **Test Data:** Valid employee fields and optional test image.
- **Steps:** Create employee; inspect details; edit; delete disposable employee.
- **Expected Result:** CRUD lifecycle succeeds; user role changes from Pending to Employee on create.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server; test Blob Storage if image supplied; delete only disposable records.

### TC-EMP-002

- **Title:** Enforce Admin-only employee management and invalid-data handling
- **Module:** Employee Management
- **Test Type:** Authorization, Validation, Negative
- **Priority:** High
- **Role:** Employee, Teacher, Student, unauthenticated; compare Admin
- **Preconditions:** Test role accounts exist.
- **Test Data:** Employee Index URL; invalid employee form with required values missing.
- **Steps:** Request Index as each role; as Admin submit invalid employee form.
- **Expected Result:** Only Admin passes class-level authorization; invalid form does not complete creation and returns validation state.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server for valid request; no data write expected from invalid submission.

## Academic Management

### TC-CLASS-001

- **Title:** Create, edit and delete a school class
- **Module:** School Class
- **Test Type:** Functional, UI, Regression
- **Priority:** High
- **Role:** Employee or Admin
- **Preconditions:** Isolated database; disposable class; no dependent rows preventing cleanup.
- **Test Data:** Unique class name, required start/end dates and optional valid course ID.
- **Steps:** Create class; edit class name/dates/course; delete disposable class with confirmation.
- **Expected Result:** List reflects create/edit/delete; course relationship is saved when selected.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server. Deletion may be restricted by student/teacher relationships; use unreferenced test class.

### TC-CLASS-002

- **Title:** Validate class required fields, length and missing IDs
- **Module:** School Class
- **Test Type:** Validation, Negative
- **Priority:** Medium
- **Role:** Employee or Admin
- **Preconditions:** Class forms available.
- **Test Data:** Empty/over-50-character ClassName; missing StartDate/EndDate; non-existing class ID.
- **Steps:** Submit invalid create/edit data; request Edit/Delete with missing ID.
- **Expected Result:** Required/max-length validation prevents valid save; missing class follows not-found path.
- **Automation Candidate:** High
- **Dependencies / Notes:** SchoolClassViewModel defines required class name, start date and end date; SQL Server for lookup.

### TC-COURSE-001

- **Title:** Create, edit, view and delete a course with class/subject associations
- **Module:** Course
- **Test Type:** Functional, Regression
- **Priority:** High
- **Role:** Employee or Admin
- **Preconditions:** Disposable class and subject exist; no dependent records prevent cleanup.
- **Test Data:** Unique name, description, duration 1–52; selected class and subject IDs.
- **Steps:** Create course with associations; inspect Details; edit associations; delete disposable course.
- **Expected Result:** Course and selected associations appear in subsequent reads; deletion completes according to FK behavior.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server; confirm cleanup ordering in isolated DB.

### TC-COURSE-002

- **Title:** Reject invalid course data and missing course ID
- **Module:** Course
- **Test Type:** Validation, Negative
- **Priority:** Medium
- **Role:** Employee or Admin
- **Preconditions:** Course form available.
- **Test Data:** Missing name; duration below 1/above 52; name over 100 or description over 500 characters; missing ID.
- **Steps:** Submit invalid create/edit; request Details/Edit/Delete with missing ID.
- **Expected Result:** Model validation errors prevent successful save; missing ID follows not-found handling.
- **Automation Candidate:** High
- **Dependencies / Notes:** Constraints are defined in CourseViewModel.

### TC-SUBJECT-001

- **Title:** Create, edit, list and delete a subject
- **Module:** Subject
- **Test Type:** Functional, UI, Regression
- **Priority:** Medium
- **Role:** Employee or Admin
- **Preconditions:** Isolated database; subject has no dependent teacher/course records at deletion time.
- **Test Data:** Unique subject name, credits and total classes.
- **Steps:** Create subject; verify Index; edit; delete disposable subject.
- **Expected Result:** Subject values persist and deleted test subject is absent from Index.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server; honor FK restrictions and clean up dependent test associations first.

### TC-SUBJECT-002

- **Title:** Validate required subject fields and name length
- **Module:** Subject
- **Test Type:** Validation, Negative
- **Priority:** Medium
- **Role:** Employee or Admin
- **Preconditions:** Subject create/edit form available.
- **Test Data:** Missing SubjectName/Credits/TotalClasses; subject name over 100 characters.
- **Steps:** Submit each invalid variant.
- **Expected Result:** Required/max-length validation is displayed and invalid form does not complete a valid save.
- **Automation Candidate:** High
- **Dependencies / Notes:** SubjectViewModel has no explicit positive Range on credits/total classes; do not assert a range rule absent another source constraint.

## Grades

### TC-GRADE-001

- **Title:** Employee/Admin creates, edits and deletes a grade
- **Module:** Grades
- **Test Type:** Functional, Regression
- **Priority:** High
- **Role:** Employee or Admin
- **Preconditions:** Disposable student and subject exist.
- **Test Data:** Grade value within 0–20; test student/subject IDs; evaluation date.
- **Steps:** Add grade; inspect student details; edit value; delete test grade.
- **Expected Result:** Grade persists through create/edit and is removed on confirmed delete; status derives as Passed at 9.5 or higher, otherwise Failed.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server; changes only to isolated test data.

### TC-GRADE-002

- **Title:** Student views only their own grades
- **Module:** Grades
- **Test Type:** Functional, Authorization
- **Priority:** High
- **Role:** Student
- **Preconditions:** Two disposable student accounts with distinct grade records exist.
- **Test Data:** Student A and Student B test identities.
- **Steps:** Sign in as each student and open `Grades/MyGrades`.
- **Expected Result:** Each student sees grades resolved for their own linked Student record, not the other test student's records.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server and login/OTP strategy; verifies data scoping as well as role attribute.

### TC-GRADE-003

- **Title:** Reject out-of-range grade and restrict grade management
- **Module:** Grades
- **Test Type:** Validation, Negative, Authorization
- **Priority:** High
- **Role:** Teacher/Student/unauthenticated for management; Employee/Admin comparison
- **Preconditions:** Grade form and role accounts available.
- **Test Data:** Grade below 0/above 20; management Index URL.
- **Steps:** Submit out-of-range grade as Employee/Admin; request management action as other roles.
- **Expected Result:** Range validation blocks save; management actions allow Employee/Admin and challenge/deny other roles.
- **Automation Candidate:** High
- **Dependencies / Notes:** Student `MyGrades` has Student-only attribute; do not infer Teacher access to grade management.

## Attendance

### TC-ATT-001

- **Title:** Employee/Admin records, edits and deletes attendance
- **Module:** Attendance
- **Test Type:** Functional, Regression
- **Priority:** High
- **Role:** Employee or Admin
- **Preconditions:** Disposable student and subject exist.
- **Test Data:** Student ID, subject ID, date and optional description.
- **Steps:** Add attendance; inspect Index/details; edit; delete test attendance.
- **Expected Result:** Attendance is saved, edited and removed; success paths redirect to the expected list/detail route.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server; use isolated records.

### TC-ATT-002

- **Title:** Student views own attendance summary
- **Module:** Attendance
- **Test Type:** Functional, Authorization
- **Priority:** High
- **Role:** Student
- **Preconditions:** Test student is linked to a user, subjects and attendance rows.
- **Test Data:** Test student with known test attendance count.
- **Steps:** Sign in as Student; open `Attendance/MyAttendances`; compare subjects/counts and summary values to isolated test data.
- **Expected Result:** Page displays the current user's attendance and computed total classes, absences and overall status.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server and OTP login. Test boundary around the source's 30% threshold using disposable data.

### TC-ATT-003

- **Title:** Restrict attendance management and validate missing student/subject
- **Module:** Attendance
- **Test Type:** Authorization, Validation, Negative
- **Priority:** High
- **Role:** Teacher/Student/unauthenticated for management; Employee/Admin comparison
- **Preconditions:** Role accounts and attendance forms available.
- **Test Data:** Missing/unknown student or subject IDs; management action URL.
- **Steps:** Request management actions under each role; submit missing/unknown references as Employee/Admin.
- **Expected Result:** Management requires Employee/Admin; invalid references follow controller validation/not-found/error handling without recording a valid attendance.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server. Student MyAttendances is separately Student-only.

## Payments

### TC-PAY-001

- **Title:** Create, view, edit and delete a payment
- **Module:** Payments
- **Test Type:** Functional, Regression
- **Priority:** High
- **Role:** Requires security policy confirmation
- **Preconditions:** Disposable student exists; isolated database.
- **Test Data:** Amount within 0.01–10000; payment date, `Pendente` status for the Pending-list check; transaction ID and method.
- **Steps:** Complete payment CRUD through MVC views; inspect Pending list using a test payment status.
- **Expected Result:** CRUD and Pending results reflect persisted test data.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server. PaymentsController has no authorization attribute; Requires security policy confirmation. Do not treat this as a confirmed bug.

### TC-PAY-002

- **Title:** Validate payment amount and bounded text fields
- **Module:** Payments
- **Test Type:** Validation, Negative
- **Priority:** Medium
- **Role:** Requires security policy confirmation
- **Preconditions:** Payment form available in isolated environment.
- **Test Data:** Amount 0/negative/above 10000; status over 20 chars; transaction ID over 50; method over 20.
- **Steps:** Submit each invalid variant.
- **Expected Result:** Data annotation/model validation prevents valid persistence for values outside declared ranges/lengths.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server. Access expectation for this controller requires security policy confirmation.

### TC-PAY-003

- **Title:** Record actual unauthenticated and wrong-role payment access behavior
- **Module:** Payments
- **Test Type:** Authorization, Negative
- **Priority:** High
- **Role:** Unauthenticated, Student, Teacher; compare intended policy
- **Preconditions:** Security owner has documented intended payment access policy; use read-only Index/Details actions.
- **Test Data:** Existing disposable payment ID.
- **Steps:** Request Index and Details without login and under each role; capture status/redirect/view and compare with approved policy.
- **Expected Result:** Mark pass/fail only after policy is confirmed; otherwise record actual behavior as unresolved.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** Requires security policy confirmation. No create/edit/delete in this access test.

## Alerts

### TC-ALERT-001

- **Title:** Employee creates alert and views employee alerts
- **Module:** Alerts
- **Test Type:** Functional, UI
- **Priority:** Medium
- **Role:** Employee
- **Preconditions:** Employee identity maps to an Employee entity; isolated database.
- **Test Data:** Unique alert message under 500 characters.
- **Steps:** Submit Create; open ViewAlerts; inspect created alert belongs to current Employee.
- **Expected Result:** Alert is saved unresolved and appears in that employee's alert list.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server. Controller checks employee mapping but has no `[Authorize]`; access policy requires confirmation.

### TC-ALERT-002

- **Title:** Resolve an alert and verify unresolved list changes
- **Module:** Alerts
- **Test Type:** Functional, Regression
- **Priority:** Medium
- **Role:** Requires security policy confirmation
- **Preconditions:** Disposable unresolved alert exists.
- **Test Data:** Test alert ID.
- **Steps:** Open unresolved alert list; invoke MarkAsResolved; reload unresolved list and inspect test alert.
- **Expected Result:** Alert is marked resolved and no longer appears in repository query for active/unresolved alerts.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server. AlertController actions have no authorization attributes; Requires security policy confirmation.

### TC-ALERT-003

- **Title:** Record alert access behavior for unauthenticated and non-Employee requests
- **Module:** Alerts
- **Test Type:** Authorization, Negative
- **Priority:** High
- **Role:** Unauthenticated, Student, Teacher; compare Employee
- **Preconditions:** Security owner confirms intended alert visibility/mutation policy; use read-only Notifications/UnresolvedAlerts/ViewAlerts.
- **Test Data:** Existing test alert data.
- **Steps:** Request listed GET actions under each role; record results against confirmed policy.
- **Expected Result:** No pass/fail expectation until policy confirmation; actual access behavior is documented.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** Requires security policy confirmation. Avoid MarkAsResolved/Create in this access-only case.

## Activity Log

### TC-LOG-001

- **Title:** Admin views activity log and non-Admin access is denied
- **Module:** Activity Log
- **Test Type:** Functional, Authorization, UI
- **Priority:** Medium
- **Role:** Admin; compare Employee and unauthenticated
- **Preconditions:** Activity log rows exist in test DB; test role accounts available.
- **Test Data:** Known disposable test log row.
- **Steps:** Open `ActivityLog/Index` as Admin, Employee and unauthenticated user.
- **Expected Result:** Admin sees logs ordered newest first; other requests are challenged/denied by class-level Admin authorization.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server; do not create production log entries.

### TC-LOG-002

- **Title:** Verify expected log entry after a supported user action
- **Module:** Activity Log
- **Test Type:** Functional, Regression
- **Priority:** Low
- **Role:** Admin (inspect); test user performs action
- **Preconditions:** Isolated DB; test user can perform a supported logged action such as profile update.
- **Test Data:** Unique test account/action.
- **Steps:** Perform action; open ActivityLog/Index as Admin; locate matching username/action/description.
- **Expected Result:** ActivityLog row is saved with the expected action and appears in newest-first order.
- **Automation Candidate:** Low
- **Dependencies / Notes:** SQL Server; this case creates audit data in test DB only.

## Dashboard

### TC-DASH-001

- **Title:** Each supported role reaches its matching dashboard
- **Module:** Dashboard
- **Test Type:** Functional, Authentication, UI, Regression
- **Priority:** Medium
- **Role:** Admin, Employee, Teacher, Student
- **Preconditions:** One disposable account per role can complete login/OTP.
- **Test Data:** Role test accounts.
- **Steps:** Complete login for each role; inspect destination and open the matching `Dashboard/*Dashboard` action.
- **Expected Result:** Login switch maps each role to its corresponding dashboard; each action accepts only its declared role.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server, SMTP and reCAPTCHA for login flow.

### TC-DASH-002

- **Title:** Challenge unauthenticated and wrong-role dashboard requests
- **Module:** Dashboard
- **Test Type:** Authorization, Negative
- **Priority:** High
- **Role:** Unauthenticated and mismatched authenticated roles
- **Preconditions:** Test accounts for the four dashboard roles exist.
- **Test Data:** Each dashboard URL with a role that should not access it.
- **Steps:** Request dashboard Index unauthenticated; request each role-specific dashboard using another role.
- **Expected Result:** Dashboard controller requires authentication; role-specific action denies/challenges identities outside its declared role.
- **Automation Candidate:** High
- **Dependencies / Notes:** ASP.NET Identity cookie challenge may redirect rather than return a raw 401/403.

## Student API

### TC-API-001

- **Title:** Get students for an existing class with students
- **Module:** Student API
- **Test Type:** API, Functional, Regression
- **Priority:** High
- **Role:** Unauthenticated (public endpoint as currently coded)
- **Preconditions:** API app running; isolated database has a class with at least one student.
- **Test Data:** Existing class ID with student rows.
- **Steps:** GET `/api/StudentsApi/BySchoolClass/{schoolClassId}`.
- **Expected Result:** HTTP 200 JSON array with student fields Id, FirstName, LastName, Status, EnrollmentDate, SchoolClassId and ImageUrl.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server. The existing Postman collection currently covers `TC-API-004`, not this successful-response scenario.

### TC-API-002

- **Title:** Get students for an existing class with no students
- **Module:** Student API
- **Test Type:** API, Negative
- **Priority:** Medium
- **Role:** Unauthenticated
- **Preconditions:** Existing class with zero students.
- **Test Data:** Class ID with no student rows.
- **Steps:** GET endpoint with that ID.
- **Expected Result:** HTTP 404 with `No students found for the specified class.`
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server; current controller treats empty result as NotFound.

### TC-API-003

- **Title:** Request students for a non-existing class ID
- **Module:** Student API
- **Test Type:** API, Negative
- **Priority:** Medium
- **Role:** Unauthenticated
- **Preconditions:** Choose ID confirmed absent in isolated database.
- **Test Data:** Non-existing integer class ID.
- **Steps:** GET endpoint with absent ID.
- **Expected Result:** HTTP 404 with `No students found for the specified class.`
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server; distinguish from existing empty class in test data/setup.

### TC-API-004

- **Title:** Reject non-integer route parameter
- **Module:** Student API
- **Test Type:** API, Negative, Validation
- **Priority:** Medium
- **Role:** Unauthenticated
- **Preconditions:** API app running.
- **Test Data:** Non-integer `schoolClassId`, for example `abc`.
- **Steps:** GET `/api/StudentsApi/BySchoolClass/abc`.
- **Expected Result:** Request does not return student data; record actual routing/model-binding status and body (ApiController automatic validation is expected to produce a client error).
- **Automation Candidate:** High
- **Dependencies / Notes:** No database write; verify actual framework response instead of assuming exact error body.

### TC-API-005

- **Title:** Verify unauthenticated access and response content type
- **Module:** Student API
- **Test Type:** API, Authentication, Authorization
- **Priority:** High
- **Role:** Unauthenticated
- **Preconditions:** Isolated class with students; intended public/private API policy documented.
- **Test Data:** Valid class ID; no cookie/token.
- **Steps:** Send GET without authentication; capture status, Content-Type and JSON structure; compare with approved policy.
- **Expected Result:** Source currently has no `[Authorize]`, so public access is expected by current configuration; if intended policy differs, mark Requires security policy confirmation.
- **Automation Candidate:** High
- **Dependencies / Notes:** SQL Server. Do not invent JWT issuance; no token-issuing endpoint was found.

## File Upload

### TC-UPLOAD-001

- **Title:** Upload valid student image in isolated storage
- **Module:** File Upload
- **Test Type:** File Upload, Functional
- **Priority:** Medium
- **Role:** Employee or Admin
- **Preconditions:** Dedicated test Blob/Azurite account/container; disposable Pending user/class; no shared storage.
- **Test Data:** Small known image file.
- **Steps:** Create student with image; verify saved ImageId and resulting image URL/readability in test storage.
- **Expected Result:** Blob is uploaded to `students` container and linked through ImageId.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server and isolated Azure Blob/Azurite. Never use shared/production storage.

### TC-UPLOAD-002

- **Title:** Upload teacher and employee images
- **Module:** File Upload
- **Test Type:** File Upload, Functional
- **Priority:** Low
- **Role:** Employee/Admin for Teacher; Admin for Employee
- **Preconditions:** Dedicated test Blob/Azurite; disposable Pending users.
- **Test Data:** Small test images for Teacher and Employee create/edit.
- **Steps:** Upload images through each relevant form; inspect ImageId/container association.
- **Expected Result:** Teacher files use `teachers`; employee files use `employees`; saved IDs resolve to expected test blob paths.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** SQL Server and isolated storage. No shared storage.

### TC-UPLOAD-003

- **Title:** Exercise empty, unsupported and oversized upload inputs
- **Module:** File Upload
- **Test Type:** File Upload, Negative, Validation
- **Priority:** Medium
- **Role:** Employee or Admin for Student/Teacher; Admin for Employee
- **Preconditions:** Dedicated test Blob/Azurite and disposable records.
- **Test Data:** No file, zero-byte file, unsupported content type/extension and file above the approved test size.
- **Steps:** Submit each upload variant through student/teacher/employee create or edit forms; inspect validation and storage.
- **Expected Result:** Record actual handling. Source explicitly skips null/zero-length files but no explicit MIME/extension/size validation was found in these upload paths; do not assume rejection without evidence.
- **Automation Candidate:** Medium
- **Dependencies / Notes:** Isolated SQL Server and Blob/Azurite only; avoid any shared storage side effects.

## Regression Candidates

Run these after a major change, using only the isolated test environment: `TC-AUTH-001`, `TC-AUTH-007`, `TC-STU-002`, `TC-TEACH-001`, `TC-CLASS-001`, `TC-GRADE-001`, `TC-ATT-001`, `TC-API-001`, `TC-DASH-001`.

## Security policy confirmation items

These cases deliberately capture behavior without declaring a confirmed defect:

- `TC-PAY-001` through `TC-PAY-003`: PaymentsController has no authorization attribute.
- `TC-ALERT-001` through `TC-ALERT-003`: AlertController has no authorization attributes; some actions check whether the current identity maps to Employee, others do not.
- `TC-AUTH-015`: profile/password-related AccountController actions have no explicit authorization attributes.
- `TC-API-005`: student API has no `[Authorize]`; confirm whether public student data is intended.
- `TC-USER-003`: ToggleStatus changes account state through an action without an explicit HTTP verb attribute.

Do not turn these observations into expected allow/deny behavior until the product/security owner confirms the intended policy.

## External dependencies

| Dependency | Cases |
|---|---|
| SQL Server / EF Core | Most account, role, CRUD, grade, attendance, payment, alert, log and API cases |
| SMTP | Login OTP, registration/confirmation, recovery, account lock/unlock and deletion OTP |
| Google reCAPTCHA | Login cases |
| Azure Blob Storage/Azurite | Image upload cases |
| Browser and session cookies | UI, authentication and dashboard cases |

## Traceability

| Test Case ID | Module | Source Controller/Action | Test Type | Priority | Automation |
|---|---|---|---|---|---|
| TC-AUTH-001–015 | Account | `AccountController`: Login, VerifyOtp, Logout, Register, ConfirmEmail, ChangeFirstPassword, ChangePassword, RecoverPassword, ResetPassword, ChangeUser | Functional/Negative/Validation/Authentication/UI | High/Medium | High/Medium |
| TC-USER-001–005 | User Management | `UserManagementController`: Index1, Edit, ToggleStatus, Delete, ConfirmDelete | Functional/Negative/Authentication/Authorization | High/Medium | Low/Medium |
| TC-STU-001–005 | Student | `StudentsController`: Index, Details, Create, Edit, DeleteConfirmed | Functional/Negative/Validation/Authorization/UI | High/Medium | High/Medium |
| TC-TEACH-001–003 | Teacher | `TeachersController`: Index, Details, Create, Edit, DeleteConfirmed | Functional/Negative/Validation/Authorization | High/Medium | High/Medium |
| TC-EMP-001–002 | Employee | `EmployeesController`: Index, Details, Create, Edit, DeleteConfirmed | Functional/Validation/Authorization | High/Medium | High/Medium |
| TC-CLASS-001–002 | School Class | `SchoolClassesController`: Index, Create, Edit, DeleteConfirmed | Functional/Negative/Validation | High/Medium | High |
| TC-COURSE-001–002 | Course | `CoursesController`: Index, Create, Details, Edit, DeleteConfirmed | Functional/Negative/Validation | High/Medium | Medium/High |
| TC-SUBJECT-001–002 | Subject | `SubjectsController`: Index, Create, Edit, DeleteConfirmed | Functional/Negative/Validation | Medium | High |
| TC-GRADE-001–003 | Grades | `GradesController`: Index, Details, AddGrade, EditGrade, DeleteGradeConfirmed, MyGrades | Functional/Negative/Validation/Authorization | High | High/Medium |
| TC-ATT-001–003 | Attendance | `AttendanceController`: Index, AddAttendance, EditAttendance, DeleteAttendanceConfirmed, MyAttendances | Functional/Negative/Validation/Authorization | High | Medium |
| TC-PAY-001–003 | Payments | `PaymentsController`: Index, Details, Create, Edit, DeleteConfirmed, Pending | Functional/Negative/Validation/Authorization | High/Medium | Medium/High |
| TC-ALERT-001–003 | Alerts | `AlertController`: Create, ViewAlerts, UnresolvedAlerts, MarkAsResolved, Notifications | Functional/Authorization/Negative | High/Medium | Medium |
| TC-LOG-001–002 | Activity Log | `ActivityLogController.Index`; `ActivityLogHelper.LogActivityAsync` | Functional/Authorization/Regression | Medium/Low | High/Low |
| TC-DASH-001–002 | Dashboard | `DashboardController`: AdminDashboard, EmployeeDashboard, TeacherDashboard, StudentDashboard | Functional/Authentication/Authorization/UI | High/Medium | High/Medium |
| TC-API-001–005 | Student API | `StudentsApiController.GetStudentsBySchoolClass` | API/Functional/Negative/Validation/Authentication/Authorization | High/Medium | High |
| TC-UPLOAD-001–003 | File Upload | Students/Teachers/Employees Create/Edit; `BlobHelper.UploadBlobAsync` | File Upload/Functional/Negative/Validation | Medium/Low | Medium |

## Source findings to verify during execution

1. `SeedDb.SeedAsync()` calls `EnsureCreatedAsync()` and conditionally inserts sample data. Never start test runs against a shared/production database without an approved startup/data strategy.
2. Login action requires reCAPTCHA, then emails a generated OTP. SMTP and CAPTCHA require test strategies.
3. `ConfirmEmail` redirects to `ChangeFirstPassword` with a newly generated temporary password. Compare this to the temporary password created and emailed during registration; a mismatch may cause the intended first-password flow to fail. This is a source observation, not an executed/confirmed defect.
4. Student/Teacher/Employee uploads create/set permissions on Blob containers. Use isolated test storage only.
5. Payment, Alert, some Account actions and Student API do not declare authorization attributes. Expected security behavior remains subject to policy confirmation.
6. `AddJwtBearer` configures token validation, but no JWT issuing implementation was found. A Postman collection exists at `docs/testing/postman/SchoolManagement_E2E.postman_collection.json` and covers selected HTTP/session workflows; no implemented Selenium suite was found.

## Design assumptions

- `<valid-id>`, test users, mailbox contents and test files must be selected/created by the future test operator in the isolated environment; this document does not access data.
- Where source contains `[Authorize]`, success/denial is expected according to the declared role; cookie middleware may express unauthenticated denial as a redirect.
- Where source has no authorization attribute and intended policy is not documented, the case records behavior and requires policy confirmation rather than assuming the current behavior is correct.
- This document defines proposed manual cases; it does not itself implement automation. The separate Postman collection includes selected scenarios and a disposable fixture setup, but does not automate all cases listed here.
