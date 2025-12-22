import { trigger } from "cs2/api";
import mod from "../../mod.json";

class TriggerBuilder {
    constructor(
        private modId: string,
        private prefix = "TRIGGER:",
    ) {}

    create<Args extends any[]>(name: string) {
        const full = `${this.prefix}${name}`;
        return (...args: Args) => {
            // cast to any[] to satisfy spread typing when Args is a tuple
            trigger(this.modId, full, ...(args as unknown as any[]));
        };
    }
}

const singleton = new TriggerBuilder(mod.id);

export default singleton;
