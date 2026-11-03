# Drawing Register

The Drawing Register records engineering documents, their revision history, and their issue and distribution state. Internal checking work is related to register documents but remains a separate concern.

## Language

### Register records

**Register**:
The authoritative project record of engineering documents and their issue history.
_Avoid_: Drawing list, document list

**Register Document**:
A drawing, certificate, letter, Document Register, or other controlled item recorded in the Register.
_Avoid_: File, Check Print

**Document Code**:
The M+J reference that identifies a Register Document independently of its revision and check-print number.
_Avoid_: Filename

**Revision**:
A named state of a Register Document that can be checked, issued, distributed, or superseded independently.
_Avoid_: Version

**Current Revision**:
The most recent dated Revision that has not been superseded.
_Avoid_: Highest revision

**Superseded Revision**:
A Revision withdrawn from current use while remaining part of the issue history.
_Avoid_: Deleted revision

**Issue**:
A dated release of a Revision with a recorded purpose, method, and issuer.
_Avoid_: Distribution

**Distribution**:
The record that an issued Revision was sent to one or more recipients.
_Avoid_: Approval

**Document Register (DocReg)**:
The SER-mandated PDF listing documents in a warrant issue. Each compiled DocReg is a Register Document, not another name for the application's Register.
_Avoid_: Register

### Checking

**Check Print**:
An internal working PDF used to record review evidence for a drawing Revision. A Check Print never becomes a Register Document.
_Avoid_: Register Document, issued drawing

**Check Print Number (CP)**:
The sequence number of a Check Print within one Document Code and Revision.
_Avoid_: Revision, version

**Check Status**:
A verdict inferred from applied PDF stamp annotations. It describes checking evidence, not issue or distribution state.
_Avoid_: Issue status

**FC**:
A Check Status meaning no stamp annotation was found. It is not proof that the drawing has not been checked.
_Avoid_: Not checked

**AWC**:
A Check Status meaning an approved-with-comments verdict stamp was found. Comments remain distinct from full approval.
_Avoid_: APPD

**APPD**:
A Check Status meaning an approved verdict stamp was found. It does not mean the Revision has been distributed.
_Avoid_: Issued

**UNKNOWN**:
A Check Status meaning stamps exist but none asserts a recognised verdict.
_Avoid_: FC

**CONFLICT**:
A Check Status meaning incompatible verdict stamps exist on the same Check Print and require human resolution.
_Avoid_: UNKNOWN

**Back-Drafted (BD)**:
An independent indication that a technician has incorporated checking comments. BD is never a Check Status.
_Avoid_: Approval
