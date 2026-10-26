import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PlatformApi } from '../../core/platform-api';
import { Plan } from '../../core/models';

@Component({
  selector: 'app-plans',
  imports: [FormsModule],
  templateUrl: './plans.html',
})
export class Plans implements OnInit {
  plans = signal<Plan[]>([]);
  loading = signal(false);
  saving = signal(false);
  errorMessage = signal<string | null>(null);

  editingId = signal<string | null>(null);
  creating = signal(false);

  formName = '';
  formMaxUsers: number | null = null;
  formMaxRecords: number | null = null;
  formPriceMonthly: number | null = null;
  formSortOrder = 0;

  constructor(private readonly api: PlatformApi) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getPlans().subscribe({
      next: (plans) => {
        this.plans.set(plans);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Could not load plans.');
        this.loading.set(false);
      },
    });
  }

  startCreate(): void {
    this.creating.set(true);
    this.editingId.set(null);
    this.formName = '';
    this.formMaxUsers = null;
    this.formMaxRecords = null;
    this.formPriceMonthly = null;
    this.formSortOrder = this.plans().length + 1;
  }

  startEdit(plan: Plan): void {
    this.creating.set(false);
    this.editingId.set(plan.id);
    this.formName = plan.name;
    this.formMaxUsers = plan.maxUsers;
    this.formMaxRecords = plan.maxRecords;
    this.formPriceMonthly = plan.priceMonthly;
    this.formSortOrder = plan.sortOrder;
  }

  cancelForm(): void {
    this.creating.set(false);
    this.editingId.set(null);
  }

  save(): void {
    if (!this.formName.trim()) {
      this.errorMessage.set('Enter a plan name.');
      return;
    }

    const payload = {
      name: this.formName.trim(),
      maxUsers: this.formMaxUsers,
      maxRecords: this.formMaxRecords,
      priceMonthly: this.formPriceMonthly,
      sortOrder: this.formSortOrder,
    };

    this.saving.set(true);
    this.errorMessage.set(null);
    const editingId = this.editingId();
    const request = editingId ? this.api.updatePlan(editingId, payload) : this.api.createPlan(payload);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.creating.set(false);
        this.editingId.set(null);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not save the plan.');
      },
    });
  }

  toggleActive(plan: Plan): void {
    this.saving.set(true);
    this.errorMessage.set(null);
    this.api.setPlanStatus(plan.id, !plan.isActive).subscribe({
      next: () => {
        this.saving.set(false);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not update the plan.');
      },
    });
  }
}
