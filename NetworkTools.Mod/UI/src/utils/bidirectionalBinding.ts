import { type ValueBinding, bindValue, trigger } from "cs2/api";
import mod from "mod.json";

export class TwoWayBinding<T> {
    public group: string;
    public id: string;
    private _binding: ValueBinding<T>;

    get bindingId(): string {
        return `BINDING:${this.id}`;
    }

    get triggerId(): string {
        return `TRIGGER:${this.id}`;
    }

    get binding(): ValueBinding<T> {
        return this._binding;
    }

    public constructor(id: string, fallbackValue?: T, group?: string) {
        this.id = id;
        this.group = group ?? mod.id;
        this._binding = bindValue<T>(this.group, this.bindingId, fallbackValue);
    }

    public set(value: T) {
        trigger(this.group, this.triggerId, value);
    }
}
