# Feature Specification: Tenant and User Access Control

**Feature Branch**: `002-tenant-and-user-access-control`

**Created**: 2026-10-28

**Status**: Draft

**Input**: User description: "Tenant and user access control for FinDoc AI. Build on the existing tenant model, roles (Tenant Administrator, Document Manager, Analyst, Auditor) and server-side authorization from feature 001. Today users and tenant memberships only exist through a development seeder; this feature lets administrators manage them."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tenant Administrator manages their team (Priority: P1)

A Tenant Administrator adds users to their own organization, assigns each a role, changes roles later, and removes people who leave. They do this themselves, without engineering help, and only ever see and affect people in their own organization.

**Why this priority**: This replaces the development-only seeding and is the minimum needed for any organization to use the product.

**Independent Test**: Sign in as a Tenant Administrator, add a user as Analyst, promote them to Document Manager, then remove them, and confirm each change takes effect immediately and affects only that organization.

**Acceptance Scenarios**:

1. **Given** a Tenant Administrator, **When** they add a new user with a role, **Then** the user can sign in and has exactly that role in that organization.
2. **Given** a member of the organization, **When** the administrator changes their role, **Then** the member's permissions change on their next request.
3. **Given** a member, **When** the administrator removes them, **Then** the member immediately loses access to all of the organization's data and documents.
4. **Given** the administrator views the member list, **Then** they see only members of their own organization, in pages.
5. **Given** an administrator, **When** they try to view, change or remove a user of another organization, **Then** the user appears not to exist.

---

### User Story 2 - Privilege and safety rules hold (Priority: P1)

The system prevents administrators from escalating privilege or locking an organization out. An organization always keeps at least one active Tenant Administrator, administrators cannot edit their own role, and no one can grant a role higher than their own.

**Why this priority**: Access control that can be bypassed or can strand an organization is worse than none; these rules are part of the core security promise.

**Independent Test**: Attempt each forbidden action (remove or demote the last administrator, change own role, grant a higher role, act across organizations) and confirm each is refused with no change.

**Acceptance Scenarios**:

1. **Given** an organization with one Tenant Administrator, **When** anyone tries to remove or demote them, **Then** the request is refused.
2. **Given** a Tenant Administrator, **When** they try to change their own role, **Then** the request is refused.
3. **Given** a Tenant Administrator, **When** they try to grant a role above their own, **Then** the request is refused.
4. **Given** a Document Manager, Analyst or Auditor, **When** they call any member-management action, **Then** they are denied.
5. **Given** open self-registration, **When** a person registers on their own, **Then** they receive no organization membership and no access to any tenant data.

---

### User Story 3 - Platform Administrator manages tenants (Priority: P2)

A Platform Administrator creates, renames and deactivates organizations and creates each organization's first Tenant Administrator. This role is separate from tenant-level administration and has no automatic access to any organization's documents or members.

**Why this priority**: Needed to onboard organizations, but only after the tenant-level rules above exist.

**Independent Test**: As a Platform Administrator, create a tenant with its first administrator, rename it, then deactivate it, confirming the new administrator can sign in and manage their tenant, while the Platform Administrator cannot read that tenant's documents.

**Acceptance Scenarios**:

1. **Given** a Platform Administrator, **When** they create a tenant with a first Tenant Administrator, **Then** that administrator can sign in and manage only that tenant.
2. **Given** a Platform Administrator, **When** they deactivate a tenant, **Then** all of its members immediately lose access to its data and documents.
3. **Given** a Platform Administrator, **When** they try to read a tenant's documents or member roles through tenant-level actions, **Then** access is denied.
4. **Given** a Tenant Administrator, **When** they try to create, rename or deactivate tenants, **Then** they are denied.

---

### User Story 4 - Users can see their own tenant and role (Priority: P3)

Any signed-in user can see which organization and role they currently have.

**Why this priority**: Helps users understand what they can do, but nothing else depends on it.

**Independent Test**: Sign in as each role and confirm the displayed organization and role are correct.

**Acceptance Scenarios**:

1. **Given** a signed-in member, **When** they ask who they are, **Then** they see their organization name and role.
2. **Given** a signed-in user with no membership, **When** they ask, **Then** they see that they have no organization access.

---

### Edge Cases

- Two administrators change the same member's role at the same time: one result wins and the member never ends with an invalid or missing role.
- Two administrators try to remove the last two administrators at the same time: at least one administrator always remains.
- A member is removed or a tenant is deactivated while they have an active session: their next request is denied.
- A user is added with an email address already used by someone in another organization: the request is refused without revealing the other organization (a user belongs to exactly one organization).
- An administrator adds an email that is already a member of their own organization: the request is refused as a duplicate.
- A removed member's documents and past audit records are retained and remain attributed to them.
- A deactivated tenant is reactivated: the previous members and roles are restored.
- A client sends a tenant identifier in a request: it is ignored; the organization always comes from the signed-in user.
- A member-list request asks for a very large page: the page size is capped.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A Tenant Administrator MUST be able to list, add, change the role of, and remove members of their own organization, and nothing outside it.
- **FR-002**: A user MUST belong to exactly one organization.
- **FR-003**: Member lists MUST be paginated with a capped page size.
- **FR-004**: Users MUST be created only through administrator action. Open self-registration MUST NOT grant any organization membership or access to tenant data.
- **FR-005**: A Platform Administrator MUST be able to create, rename, deactivate and reactivate organizations and create the first Tenant Administrator of an organization.
- **FR-006**: The Platform Administrator role MUST be separate from tenant roles and MUST NOT grant access to any organization's documents or member data.
- **FR-007**: Deactivating an organization or removing a member MUST immediately deny that organization's data and documents (including the feature 001 document actions) to the affected users.
- **FR-008**: An organization MUST always have at least one active Tenant Administrator; removing or demoting the last one MUST be refused, including under concurrent requests.
- **FR-009**: A user MUST NOT be able to change their own role.
- **FR-010**: A user MUST NOT be able to grant a role above their own.
- **FR-011**: The organization MUST be determined from the signed-in user on the server and MUST NEVER be accepted from the request. Another organization's users MUST appear not to exist.
- **FR-012**: Every organization, membership and role change MUST be recorded as an audit event with the actor, organization, action, target and time, containing no passwords or tokens.
- **FR-013**: Roles and membership MUST be checked on every request so that changes and removals take effect immediately, not at next sign-in.
- **FR-014**: Any signed-in user MUST be able to see their own organization and role.
- **FR-015**: Authorization MUST be enforced by the system on every request, not only by the web interface.
- **FR-016**: Removing a member MUST NOT delete the documents they uploaded or alter existing audit records.
- **FR-017**: External identity providers, email delivery, password-reset flows, billing, per-document permissions, and an audit-viewing interface MUST NOT be part of this feature.

### Key Entities *(include if feature involves data)*

- **Tenant (Organization)**: An isolated customer organization with a name and an active or deactivated state.
- **User**: A person who can sign in, belonging to at most one organization.
- **Membership**: The link between a user, an organization and a role.
- **Role**: Tenant Administrator, Document Manager, Analyst or Auditor within an organization, plus the separate Platform Administrator role.
- **Audit Event**: A record of an organization, membership or role change with actor, organization, action, target and time.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In automated tests, a Tenant Administrator of one organization cannot list, add, change or remove users of another organization in 100% of attempts.
- **SC-002**: In automated tests, 100% of privilege-escalation attempts (granting a higher role, changing own role, non-administrators calling member management) are refused with no change.
- **SC-003**: Tests confirm that an organization can never end with zero active Tenant Administrators, including when requests arrive at the same time.
- **SC-004**: Removed members and members of deactivated organizations are denied on their very next request to every feature 001 document action.
- **SC-005**: Tests confirm that self-registered users have no organization and no access to any tenant data.
- **SC-006**: A Platform Administrator cannot read any organization's documents or member lists in 100% of tested attempts.
- **SC-007**: Every membership, role and organization change produces a matching audit event in 100% of tested cases, with no secrets.
- **SC-008**: A Tenant Administrator can add a new user and have them signed in and working within 2 minutes, without engineering help.

## Assumptions

- Feature 001 is in place: organizations, the four tenant roles, document permissions and audit events already exist.
- A user belongs to exactly one organization; supporting several is out of scope.
- Administrators create users directly and set an initial credential for them to change; email invitations and password reset are out of scope.
- Role ranking, highest to lowest: Tenant Administrator, Document Manager, Analyst, Auditor. Only Tenant Administrators can manage members.
- A Platform Administrator is created through a controlled operational step, not through the public interface.
- Deactivation is reversible; permanent deletion of an organization or its data is out of scope and follows the retention decision still pending for feature 001.
- Removed members keep their historical attribution in documents and audit records.
