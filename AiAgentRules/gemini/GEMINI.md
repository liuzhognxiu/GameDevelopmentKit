## Gemini Added Memories
- Do not delete original code after generating new code.
- Subclasses of AUGuiForm should import the 'Game' namespace, but not the 'Game.UI' namespace.
- 项目框架使用约定：将初始化数据通过 'object userData' 参数传递给实体和UI。
- The user's excel files are configured in g:\GameDevelopmentKitAI\Design\Excel
- The user's table data is in the g:\GameDevelopmentKitAI\Design path.
- The `__enums__.xlsx` file defines enums. Each enum item is on a new row. The enum's `full_name` is in column B of the first item's row. Subsequent items for the same enum have an empty column B. Item properties like `name` and `value` are in columns H onwards.
- When adding an enum in `__enums__.xlsx`, if the items are unique, set the `unique` flag in column D to `TRUE` for the first item of the enum.
- In `Weapon.xlsx`, for enum-type columns, the cell value should be the alias of the enum member as defined in `__enums__.xlsx`, not the member's name.
- Permanent memories can only be added, not deleted.
- Subclasses of AUGuiForm should import the 'Game' namespace, but not the 'Game.UI' namespace.
- Project framework convention: Pass initialization data to entities and UI via the 'object userData' parameter.
- Do not delete original code after generating new code.
- In `__enums__.xlsx`, each enum item is on a new row. The enum's `full_name` is in column B of the first item's row. Subsequent items for the same enum have an empty column B.
- Table Modification Rule: When modifying Excel tables, first analyze the table structure (`##var`, `##type`, `##` rows). Strictly follow the specific rules for each table, such as the `full_name` and `unique` conventions in `__enums__.xlsx`, and using the alias for enum columns in other tables like `Weapon.xlsx`.
ng new classes that implement existing interfaces.
- Communication Workflow Rule: 1. Clarify and Confirm: When in doubt, ask for clarification. Present a plan for approval before making significant changes. 2. Handle Errors: If an operation fails, inform the user and ask for help to resolve it. 3. Learn from mistakes: If a change is incorrect, learn from feedback, correct the mistake, and update internal rules to avoid repeating it.
- Final Development Workflow: When adding a new item/resource (e.g., a new bullet type): 1. Resources: First, handle the resource creation. The user will either instruct me to create a prefab or they will create it themselves and I can see the changes via `git diff`. The resources are located in `g:\GameDevelopmentKitAI\Unity\Assets\Res`. 2. Entity Table: Add an entry for the new item in `Entity.xlsx` to define its entity ID. 3. Enum Table: If applicable, add a new enum for the item type in `__enums__.xlsx`. 4. Data Generation: After the table modifications are complete, I must notify the user to run the `gen all` script. I will then wait for the user to confirm that the generation is complete. 5. Compilation: After data generation, the user will compile the project. I will wait for the user to confirm that the compilation was successful. 6. Scripting: Only after the above steps are complete, I can start writing the C# script logic that uses the newly generated types and resources.
