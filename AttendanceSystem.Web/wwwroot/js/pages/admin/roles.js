/* ── Admin Roles Management JavaScript ── */

var currentRoleId = 0;
var allRoles = [];

/* Everything the server returned for the selected role — the save guard checks against it. */
var loadedPermissions = [];

document.addEventListener('DOMContentLoaded', function () {
    loadRoles();
});

function loadRoles() {
    fetch('/api/roles')
        .then(res => res.json())
        .then(data => {
            allRoles = data || [];
            renderRoleList(allRoles);
            if (allRoles.length > 0 && currentRoleId === 0) {
                var firstId = getRoleId(allRoles[0]);
                selectRole(firstId);
            }
        })
        .catch(err => {
            console.error(err);
            showAlert('danger', 'Error fetching system roles.');
        });
}

function getRoleId(r) {
    if (!r) return 0;
    return r.id !== undefined ? r.id : (r.Id !== undefined ? r.Id : 0);
}

function getRoleName(r) {
    if (!r) return 'Role';
    return r.name || r.Name || 'Role';
}

function getRoleDesc(r) {
    if (!r) return '';
    return r.description || r.Description || 'System access role';
}

function renderRoleList(roles) {
    var container = document.getElementById('roleList');
    if (!roles || roles.length === 0) {
        container.innerHTML = '<div class="p-3 text-muted">No roles found.</div>';
        return;
    }
    var html = '';
    roles.forEach(r => {
        var rid = getRoleId(r);
        var rname = getRoleName(r);
        var rdesc = getRoleDesc(r);

        var activeClass = (rid === currentRoleId) ? 'active' : '';
        var bgStyle = (rid === currentRoleId) ? 'background:#01a9ac !important;color:#fff !important;' : '';
        html += `
            <a href="#" class="list-group-item list-group-item-action ${activeClass}" style="${bgStyle}" onclick="selectRole(${rid}); return false;">
                <div class="d-flex w-100 justify-content-between align-items-center">
                    <h6 class="mb-1 fw-bold">${rname}</h6>
                    <small class="badge bg-secondary">ID: ${rid}</small>
                </div>
                <p class="mb-0 text-truncate" style="font-size:.82rem;opacity:.9;">${rdesc}</p>
            </a>
        `;
    });
    container.innerHTML = html;
}

function selectRole(roleId) {
    currentRoleId = roleId;
    renderRoleList(allRoles);

    var role = allRoles.find(r => getRoleId(r) === roleId);
    if (role) {
        var rname = getRoleName(role);
        var rdesc = getRoleDesc(role);
        document.getElementById('selectedRoleTitle').innerHTML = '<i class="fa fa-shield me-2" style="color:#01a9ac;"></i>' + rname + ' Permission Matrix';
        document.getElementById('selectedRoleDesc').innerText = rdesc || 'Page-level & Transaction-level permissions.';
        document.getElementById('roleActions').style.display = 'block';
    }

    fetch('/api/roles/' + roleId + '/permissions')
        .then(res => res.json())
        .then(perms => {
            loadedPermissions = perms || [];
            renderMatrixTable(loadedPermissions);
        })
        .catch(err => {
            console.error(err);
            showAlert('danger', 'Failed to load permissions for role.');
        });
}

/*
 * Column order and styling for the actions we know about. Anything not listed still gets a
 * column — see buildActionColumns — so a new action can never go missing from this screen again.
 */
var ACTION_META = {
    View:              { label: 'Page Access', colour: 'primary'   },
    Create:            { label: 'Create',      colour: 'success'   },
    Edit:              { label: 'Save',        colour: 'success'   },
    Delete:            { label: 'Remove',      colour: 'danger'    },
    Approve:           { label: 'Authorise',   colour: 'warning'   },
    Export:            { label: 'Export',      colour: 'info'      },
    Sync:              { label: 'Sync device', colour: 'secondary' },
    Assign:            { label: 'Assign',      colour: 'secondary' },
    BulkImport:        { label: 'Bulk import', colour: 'secondary' },
    AssignPermissions: { label: 'Grant rights', colour: 'secondary' }
};
var ACTION_ORDER = ['View', 'Create', 'Edit', 'Delete', 'Approve', 'Export', 'Sync',
                    'Assign', 'BulkImport', 'AssignPermissions'];

/*
 * The set of columns is derived from the permissions the server actually returned, not from a
 * fixed list.
 *
 * The previous fixed list was ['View','Add','Edit','Delete','Print']. Two of those names do not
 * exist in this system — the real actions are Create and Export — and Approve, Sync, Assign,
 * BulkImport and AssignPermissions had no column at all. 21 of 63 permissions were therefore
 * invisible, and because saving submits only the rendered checkboxes and the server replaces a
 * role's permissions wholesale, pressing Save silently revoked every one of them.
 */
function buildActionColumns(perms) {
    var present = {};
    perms.forEach(p => { present[p.action || p.Action || 'View'] = true; });

    var known = ACTION_ORDER.filter(a => present[a]);
    var extra = Object.keys(present).filter(a => ACTION_ORDER.indexOf(a) < 0).sort();
    return known.concat(extra);
}

function renderMatrixTable(perms) {
    var container = document.getElementById('permissionMatrixContainer');
    if (!perms || perms.length === 0) {
        container.innerHTML = '<div class="p-4 text-center text-muted">No permission definitions found.</div>';
        return;
    }

    // Group permissions by Module
    var modulesMap = {};
    perms.forEach(p => {
        var m = p.module || p.Module || 'General';
        var a = p.action || p.Action || 'View';
        var pid = p.id !== undefined ? p.id : p.Id;
        var granted = p.isGranted !== undefined ? p.isGranted : p.IsGranted;

        if (!modulesMap[m]) modulesMap[m] = {};
        modulesMap[m][a] = { id: pid, isGranted: granted, displayName: p.displayName || p.DisplayName };
    });

    var actionsList = buildActionColumns(perms);

    var headCells = actionsList.map(act => {
        var meta = ACTION_META[act] || { label: act, colour: 'secondary' };
        return `<th class="text-center" style="width:96px;">
                    <span class="badge bg-${meta.colour}">${esc(act)}</span><br>
                    <span style="font-size:.72rem;">${esc(meta.label)}</span>
                </th>`;
    }).join('');

    var html = `
        <div class="table-responsive">
            <table class="table table-hover align-middle mb-0" style="font-size:.9rem;">
                <thead class="table-light">
                    <tr>
                        <th style="min-width:200px;" class="ps-3">System Page / Module</th>
                        ${headCells}
                        <th class="text-center" style="width:100px;">Toggle Row</th>
                    </tr>
                </thead>
                <tbody>
    `;

    Object.keys(modulesMap).forEach(moduleName => {
        var mod = modulesMap[moduleName];
        var safeModId = moduleName.replace(/[^a-zA-Z0-9]/g, '_');

        html += `<tr>`;
        html += `<td class="ps-3 fw-bold text-dark"><i class="fa fa-folder-o me-2" style="color:#01a9ac;"></i>${esc(moduleName)}</td>`;

        actionsList.forEach(act => {
            var item = mod[act];
            if (item) {
                var chk = item.isGranted ? 'checked' : '';
                html += `
                    <td class="text-center">
                        <input class="form-check-input perm-chk row-${safeModId} col-${act}"
                               type="checkbox" value="${item.id}" id="perm_${item.id}" ${chk} style="width:18px;height:18px;cursor:pointer;">
                    </td>
                `;
            } else {
                html += `<td class="text-center text-muted">—</td>`;
            }
        });

        html += `
            <td class="text-center">
                <button class="btn btn-xs btn-outline-secondary py-0 px-2" style="font-size:.78rem;" onclick="toggleRow('${safeModId}')">
                    Row
                </button>
            </td>
        `;
        html += `</tr>`;
    });

    html += `
                </tbody>
            </table>
        </div>
    `;

    container.innerHTML = html;
}

function toggleRow(safeModId) {
    var chks = document.querySelectorAll('.row-' + safeModId);
    var anyUnchecked = Array.from(chks).some(c => !c.checked);
    chks.forEach(c => c.checked = anyUnchecked);
}

function toggleAllPermissions(check) {
    document.querySelectorAll('.perm-chk').forEach(c => c.checked = check);
}

function saveRolePermissions() {
    if (!currentRoleId) {
        showAlert('warning', 'Please select a role first.');
        return;
    }

    /*
     * Saving replaces a role's permissions wholesale on the server, so an unchecked box and a
     * permission that was never drawn are indistinguishable in the payload — both come out as
     * "revoke". That is exactly how the old fixed column list silently stripped 21 permissions
     * on every save. Refuse rather than guess if the matrix does not cover everything loaded.
     */
    var rendered = document.querySelectorAll('.perm-chk').length;
    if (loadedPermissions.length && rendered !== loadedPermissions.length) {
        showAlert('danger',
            'Not saving: the matrix is showing ' + rendered + ' of ' + loadedPermissions.length +
            ' permissions, so saving would revoke the ones it cannot show. Reload the page and ' +
            'report this if it persists.');
        return;
    }

    var selectedIds = [];
    document.querySelectorAll('.perm-chk:checked').forEach(c => {
        selectedIds.push(parseInt(c.value));
    });

    fetch('/api/roles/' + currentRoleId + '/permissions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ permissionIds: selectedIds })
    })
    .then(res => {
        if (res.ok) {
            showAlert('success', 'Transaction & page access rules updated successfully!');
        } else {
            showAlert('danger', 'Failed to update permissions.');
        }
    })
    .catch(err => {
        console.error(err);
        showAlert('danger', 'Error connecting to server.');
    });
}

function openAddRoleModal() {
    document.getElementById('roleId').value = 0;
    document.getElementById('roleName').value = '';
    document.getElementById('roleDescription').value = '';
    document.getElementById('roleModalTitle').innerText = 'Add System Role';
    new bootstrap.Modal(document.getElementById('roleModal')).show();
}

function saveRole() {
    var id = parseInt(document.getElementById('roleId').value);
    var name = document.getElementById('roleName').value.trim();
    var desc = document.getElementById('roleDescription').value.trim();

    if (!name) { alert('Role title is required.'); return; }

    fetch('/api/roles', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: id, name: name, description: desc })
    })
    .then(res => res.json())
    .then(data => {
        bootstrap.Modal.getInstance(document.getElementById('roleModal')).hide();
        showAlert('success', 'Role saved successfully.');
        loadRoles();
    })
    .catch(err => {
        console.error(err);
        showAlert('danger', 'Failed to save role.');
    });
}

function showAlert(type, message) {
    var container = document.getElementById('alertContainer');
    container.innerHTML = `
        <div class="alert alert-${type} alert-dismissible fade show mb-3" role="alert">
            <i class="fa fa-${type === 'success' ? 'check-circle' : 'exclamation-triangle'} me-2"></i>${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `;
}
