/* ── Payroll Setup: Employment Categories ─────────────────────────────────────

   Its own file rather than appended to payroll-setup.js, which is already long.
   Uses psField, psShowModal, psPost and psDelete from there, so it must load after it. */

var psCategories = [];

$(function () { psLoadCategories(); });

function psLoadCategories() {
    $.getJSON('/api/payroll-setup/categories', function (d) {
        psCategories = d || [];
        amsPage('#psCategoryBody', psCategories, function (c) {
            return '<tr>'
                 + '<td class="ps-3 fw-semibold">' + esc(c.Code) + '</td>'
                 + '<td>' + esc(c.Name) + '</td>'
                 // Stated as words rather than a tick: "no EPF/ETF" is a consequence worth
                 // reading at a glance, not a flag to decode.
                 + '<td class="text-center">' + (c.IsEpfEligible
                        ? '<span class="badge bg-success">Yes</span>'
                        : '<span class="badge bg-secondary">No</span>') + '</td>'
                 + '<td class="text-center"><span class="badge bg-secondary">'
                 + esc(c.EmployeeCount) + '</span></td>'
                 + '<td class="text-center">' + psStatus(c.IsActive) + '</td>'
                 + '<td class="text-end pe-3">'
                 + psActions('psCategoryModal', c.Id, 'psCategoryDelete') + '</td>'
                 + '</tr>';
        }, { colspan: 6, empty: 'No employment categories defined.', label: 'category' });
    });
}

function psCategoryModal(id) {
    var c = psCategories.filter(function (x) { return x.Id === id; })[0]
         || { IsEpfEligible: true, IsActive: true };

    psShowModal(id ? 'Edit Category' : 'Add Category',
        psField('Code', 'ecCode', 'text', c.Code, { required: true, maxlength: 20, col: 4 })
      + psField('Name', 'ecName', 'text', c.Name, { required: true, maxlength: 100, col: 8 })
      + psField('EPF eligible', 'ecEpf', 'checkbox', c.IsEpfEligible, {
            col: 6, checkLabel: 'Joins EPF and ETF by default',
            help: 'A default for new employees. It does not change anyone already assigned — '
                + 'their membership switches stay as they are.'
        })
      + psField('Active', 'ecActive', 'checkbox', c.IsActive, { col: 6, checkLabel: 'Active' }),
        function () {
            var dto = {
                Id: id || 0,
                Code: $('#ecCode').val().trim(),
                Name: $('#ecName').val().trim(),
                IsEpfEligible: $('#ecEpf').is(':checked'),
                IsActive: $('#ecActive').is(':checked')
            };
            if (!dto.Code || !dto.Name) { notifyError('Code and name are required.'); return; }
            psPost('/api/payroll-setup/categories', dto, 'Category', psLoadCategories);
        });
}

function psCategoryDelete(id) {
    psDelete('/api/payroll-setup/categories/' + id, 'category', psLoadCategories);
}
