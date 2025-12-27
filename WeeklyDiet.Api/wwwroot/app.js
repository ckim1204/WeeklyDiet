const api = {
    ingredients: '/api/ingredients',
    foods: '/api/foods',
    currentPlan: '/api/plans/current',
    upcomingPlan: '/api/plans/upcoming'
};

const state = {
    ingredients: [],
    foods: [],
    plans: {
        current: null,
        upcoming: null
    },
    selectedPlanKey: 'current',
    editingFoodId: null,
    editingIngredientId: null,
    ingredientFilter: '',
    foodFilter: '',
    foodIngredientFilter: '',
    selectedFoodIngredientIds: []
};

const statusBadge = document.getElementById('statusBadge');
const ingredientForm = document.getElementById('ingredientForm');
const ingredientNameInput = document.getElementById('ingredientName');
const ingredientList = document.getElementById('ingredientList');
const ingredientMode = document.getElementById('ingredientMode');
const ingredientSubmit = document.getElementById('ingredientSubmit');
const ingredientCancel = document.getElementById('ingredientCancel');
const ingredientSearch = document.getElementById('ingredientSearch');

const foodForm = document.getElementById('foodForm');
const foodNameInput = document.getElementById('foodName');
const foodList = document.getElementById('foodList');
const foodMode = document.getElementById('foodMode');
const foodSubmit = document.getElementById('foodSubmit');
const foodCancel = document.getElementById('foodCancel');
const foodSearch = document.getElementById('foodSearch');
const foodIngredientSearch = document.getElementById('foodIngredientSearch');
const foodIngredientCheckboxes = document.getElementById('foodIngredientCheckboxes');
const addFoodBtn = document.getElementById('addFoodBtn');
const foodBackBtn = document.getElementById('foodBackBtn');
const foodEditorTitle = document.getElementById('foodEditorTitle');

const planContainer = document.getElementById('planContainer');
const groceryList = document.getElementById('groceryList');

document.querySelectorAll('.nav-btn').forEach(btn => {
    btn.addEventListener('click', () => {
        document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        const view = btn.dataset.view;
        document.querySelectorAll('.view').forEach(v => v.classList.add('hidden'));
        if (view === 'foods') {
            showFoodList();
        }
        document.querySelector(`.view[data-view="${view}"]`).classList.remove('hidden');
    });
});

document.querySelectorAll('.tab').forEach(tab => {
    tab.addEventListener('click', () => {
        document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
        tab.classList.add('active');
        state.selectedPlanKey = tab.dataset.plan;
        renderPlan();
        renderGroceryList();
    });
});

document.getElementById('refreshPlans').addEventListener('click', async () => {
    await loadPlans();
});

document.getElementById('generateCurrent').addEventListener('click', async () => {
    await callApi('/api/plans/current/generate', { method: 'POST' });
    await loadPlans();
});

document.getElementById('generateUpcoming').addEventListener('click', async () => {
    await callApi('/api/plans/upcoming/generate', { method: 'POST' });
    await loadPlans();
});

ingredientForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    const name = ingredientNameInput.value.trim();
    if (!name) return;

    const payload = { name };
    if (state.editingIngredientId) {
        await callApi(`${api.ingredients}/${state.editingIngredientId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
    } else {
        await callApi(api.ingredients, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
    }

    resetIngredientForm();
    await loadIngredients();
    await loadFoods();
});

ingredientCancel.addEventListener('click', () => resetIngredientForm());
ingredientSearch.addEventListener('input', (e) => {
    state.ingredientFilter = e.target.value.toLowerCase();
    renderIngredients();
});

foodForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    const name = foodNameInput.value.trim();
    const ingredientIds = getSelectedFoodIngredientIds();
    const allowedMealTypes = Array.from(document.querySelectorAll('#mealTypeGroup input[type=checkbox]:checked')).map(c => c.value);

    if (!name || ingredientIds.length === 0 || allowedMealTypes.length === 0) {
        alert('Name, ingredients, and meal types are required.');
        return;
    }

    const payload = { name, ingredientIds, allowedMealTypes };
    if (state.editingFoodId) {
        await callApi(`${api.foods}/${state.editingFoodId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
    } else {
        await callApi(api.foods, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
    }

    resetFoodForm();
    await loadFoods();
    await loadPlans();
    showFoodList();
});

foodCancel.addEventListener('click', () => {
    resetFoodForm();
    showFoodList();
});
foodSearch.addEventListener('input', (e) => {
    state.foodFilter = e.target.value.toLowerCase();
    renderFoods();
});
foodIngredientSearch.addEventListener('input', (e) => {
    state.foodIngredientFilter = e.target.value.toLowerCase();
    renderIngredientCheckboxes();
});
addFoodBtn.addEventListener('click', () => {
    resetFoodForm();
    setFoodIngredientSelection([]);
    showFoodEditor();
});
foodBackBtn.addEventListener('click', () => {
    showFoodList();
});

async function callApi(url, options = {}) {
    setStatus('Working...');
    try {
        const res = await fetch(url, options);
        if (!res.ok) {
            const msg = await res.text();
            throw new Error(msg || res.statusText);
        }
        setStatus('Ready');
        const contentType = res.headers.get('content-type') || '';
        return contentType.includes('application/json') ? res.json() : res.text();
    } catch (err) {
        console.error(err);
        setStatus('Error');
        alert(err.message || 'Something went wrong');
        throw err;
    }
}

async function getJsonOrNull(url) {
    const res = await fetch(url);
    if (res.status === 404) return null;
    if (!res.ok) {
        const msg = await res.text();
        throw new Error(msg || res.statusText);
    }
    return res.json();
}

function setStatus(text) {
    statusBadge.textContent = text;
}

async function loadIngredients() {
    const data = await callApi(api.ingredients);
    state.ingredients = data;
    renderIngredients();
    renderIngredientCheckboxes();
}

function renderIngredients() {
    ingredientList.innerHTML = '';
    state.ingredients
        .filter(i => !state.ingredientFilter || i.name.toLowerCase().includes(state.ingredientFilter))
        .forEach(item => {
            const li = document.createElement('li');
            li.textContent = item.name;

            const editBtn = document.createElement('button');
            editBtn.textContent = 'Edit';
            editBtn.addEventListener('click', () => {
                state.editingIngredientId = item.id;
                ingredientNameInput.value = item.name;
                ingredientMode.textContent = 'Edit mode';
                ingredientSubmit.textContent = 'Update';
                ingredientCancel.classList.remove('hidden');
            });

            const delBtn = document.createElement('button');
            delBtn.textContent = 'Delete';
            delBtn.classList.add('danger');
            delBtn.addEventListener('click', async () => {
                if (!confirm('Delete ingredient?')) return;
                await callApi(`${api.ingredients}/${item.id}`, { method: 'DELETE' });
                await loadIngredients();
                await loadFoods();
            });

            li.append(editBtn, delBtn);
            ingredientList.appendChild(li);
        });
}

function renderIngredientCheckboxes() {
    const currentSelections = new Set(state.selectedFoodIngredientIds.map(Number));
    const filtered = state.ingredients.filter(i =>
        !state.foodIngredientFilter || i.name.toLowerCase().includes(state.foodIngredientFilter));

    foodIngredientCheckboxes.innerHTML = '';
    filtered.forEach(i => {
        const idNum = Number(i.id);
        const label = document.createElement('label');
        const input = document.createElement('input');
        input.type = 'checkbox';
        input.value = idNum;
        input.checked = currentSelections.has(idNum);
        input.addEventListener('change', () => {
            if (input.checked) {
                state.selectedFoodIngredientIds = Array.from(new Set([...state.selectedFoodIngredientIds, idNum]));
            } else {
                state.selectedFoodIngredientIds = state.selectedFoodIngredientIds.filter(x => x !== idNum);
            }
        });
        label.append(input, document.createTextNode(i.name));
        foodIngredientCheckboxes.appendChild(label);
    });

    state.selectedFoodIngredientIds = Array.from(currentSelections);
}

function getSelectedFoodIngredientIds() {
    const ids = new Set(state.selectedFoodIngredientIds);
    return Array.from(ids);
}

async function loadFoods() {
    const data = await callApi(api.foods);
    state.foods = data;
    renderFoods();
}

function renderFoods() {
    foodList.innerHTML = '';
    state.foods
        .filter(f => {
            if (!state.foodFilter) return true;
            const matchName = f.name.toLowerCase().includes(state.foodFilter);
            const matchIngredient = f.ingredients.some(i => i.name.toLowerCase().includes(state.foodFilter));
            return matchName || matchIngredient;
        })
        .forEach(food => {
            const card = document.createElement('div');
            card.className = 'card';
            const header = document.createElement('header');
            const title = document.createElement('div');
            title.textContent = food.name;
            const chip = document.createElement('div');
            chip.className = 'chip';
            chip.textContent = food.allowedMealTypes.join(', ');
            header.append(title, chip);

            const accordion = document.createElement('details');
            accordion.className = 'accordion';
            const summary = document.createElement('summary');
            summary.textContent = 'Ingredients';
            accordion.appendChild(summary);
            const tagsContainer = document.createElement('div');
            tagsContainer.className = 'tags';
            food.ingredients.forEach(i => {
                const tag = document.createElement('span');
                tag.className = 'tag';
                tag.textContent = i.name;
                tagsContainer.appendChild(tag);
            });
            accordion.appendChild(tagsContainer);

            const actions = document.createElement('div');
            actions.className = 'cell-actions';

            const editBtn = document.createElement('button');
            editBtn.textContent = 'Edit';
            editBtn.addEventListener('click', () => {
                state.editingFoodId = food.id;
                foodNameInput.value = food.name;
                setFoodIngredientSelection(food.ingredientIds);
                const checkboxes = document.querySelectorAll('#mealTypeGroup input[type=checkbox]');
                checkboxes.forEach(cb => {
                    cb.checked = food.allowedMealTypes.includes(cb.value);
                });
                foodMode.textContent = 'Edit mode';
                foodSubmit.textContent = 'Update Food';
                foodCancel.classList.remove('hidden');
                foodEditorTitle.textContent = `Edit: ${food.name}`;
                showFoodEditor();
            });

            const delBtn = document.createElement('button');
            delBtn.textContent = 'Delete';
            delBtn.classList.add('danger');
            delBtn.addEventListener('click', async () => {
                if (!confirm('Delete food?')) return;
                await callApi(`${api.foods}/${food.id}`, { method: 'DELETE' });
                await loadFoods();
                await loadPlans();
            });

            actions.append(editBtn, delBtn);
            card.append(header, accordion, actions);
            foodList.appendChild(card);
        });
}

function showFoodEditor() {
    document.querySelectorAll('.view').forEach(v => {
        if (v.dataset.view === 'food-editor') {
            v.classList.remove('hidden');
        } else if (v.dataset.view === 'foods') {
            v.classList.add('hidden');
        }
    });
}

function showFoodList() {
    document.querySelectorAll('.view').forEach(v => {
        if (v.dataset.view === 'food-editor') {
            v.classList.add('hidden');
        } else if (v.dataset.view === 'foods') {
            v.classList.remove('hidden');
        }
    });
    resetFoodForm();
    foodEditorTitle.textContent = 'Food Editor';
    setFoodIngredientSelection([]);
}

async function loadPlans() {
    const [current, upcoming] = await Promise.all([
        getJsonOrNull(api.currentPlan),
        getJsonOrNull(api.upcomingPlan)
    ]);
    state.plans.current = current;
    state.plans.upcoming = upcoming;
    renderPlan();
    renderGroceryList();
}

function renderPlan() {
    planContainer.innerHTML = '';
    const plan = state.plans[state.selectedPlanKey];
    if (!plan) {
        const div = document.createElement('div');
        div.textContent = 'No plan yet. Click generate to create one.';
        planContainer.appendChild(div);
        return;
    }

    const template = document.getElementById('planTemplate');
    const clone = template.content.cloneNode(true);
    clone.querySelector('.plan-week').textContent = plan.weekLabel;
    clone.querySelector('.plan-dates').textContent = `${plan.startDate} -> ${plan.endDate}`;

    const tbody = clone.querySelector('tbody');
    const mealRows = ['Breakfast', 'Lunch', 'Dinner'].map(mt => {
        const tr = document.createElement('tr');
        const th = document.createElement('th');
        th.textContent = mt;
        tr.appendChild(th);
        for (let day = 1; day <= 7; day++) {
            const td = document.createElement('td');
            const entry = plan.meals.find(m => m.dayOfWeek === day && m.mealType === mt);
            if (entry) {
                td.appendChild(renderMealCell(plan, entry));
            } else {
                td.textContent = '-';
            }
            tr.appendChild(td);
        }
        return tr;
    });

    mealRows.forEach(r => tbody.appendChild(r));
    planContainer.appendChild(clone);
}

function renderMealCell(plan, entry) {
    const cell = document.createElement('div');
    cell.className = 'meal-cell';

    const title = document.createElement('div');
    title.className = 'meal-title';
    title.textContent = entry.foodName || 'Unassigned';
    if (entry.isLeftover) {
        const leftoverTag = document.createElement('span');
        leftoverTag.className = 'meal-meta';
        leftoverTag.textContent = ' (Leftover)';
        title.appendChild(leftoverTag);
    }
    cell.appendChild(title);

    const actions = document.createElement('div');
    actions.className = 'cell-actions';

    const replaceBtn = document.createElement('button');
    replaceBtn.textContent = 'Replace';
    replaceBtn.addEventListener('click', () => replaceMeal(plan, entry));
    actions.appendChild(replaceBtn);

    if (state.selectedPlanKey === 'current') {
        const isSourceForLeftover = plan.meals.some(m => m.leftoverFromMealEntryId === entry.id);
        const leftoverBtn = document.createElement('button');
        leftoverBtn.textContent = isSourceForLeftover ? 'Clear leftover' : 'Use as leftover';
        leftoverBtn.addEventListener('click', () => toggleLeftover(plan, entry, !isSourceForLeftover));
        actions.appendChild(leftoverBtn);
    }

    cell.appendChild(actions);
    return cell;
}

async function replaceMeal(plan, entry) {
    const options = state.foods.filter(f => f.allowedMealTypes.includes(entry.mealType));
    if (options.length === 0) {
        alert('Add foods for this meal type first.');
        return;
    }
    const choice = prompt(`Choose option for ${entry.mealType}:\n${options.map((o, idx) => `${idx + 1}. ${o.name}`).join('\n')}`, '1');
    if (!choice) return;
    const idx = parseInt(choice, 10) - 1;
    const selected = options[idx];
    if (!selected) return;

    await callApi(`/api/plans/${plan.year}/${plan.weekNumber}/days/${entry.dayOfWeek}/meals/${entry.mealType}/replace?foodId=${selected.id}`, {
        method: 'POST'
    });
    await loadPlans();
}

async function toggleLeftover(plan, entry, isLeftover) {
    await callApi(`/api/plans/${plan.year}/${plan.weekNumber}/days/${entry.dayOfWeek}/meals/${entry.mealType}/leftover?isLeftover=${isLeftover}`, {
        method: 'POST'
    });
    await loadPlans();
}

async function renderGroceryList() {
    groceryList.innerHTML = '';
    const plan = state.plans[state.selectedPlanKey];
    if (!plan) {
        const li = document.createElement('li');
        li.textContent = 'No plan yet.';
        groceryList.appendChild(li);
        return;
    }

    const data = await getJsonOrNull(`/api/plans/${plan.year}/${plan.weekNumber}/grocery-list`);
    if (!data) {
        const li = document.createElement('li');
        li.textContent = 'No grocery list yet.';
        groceryList.appendChild(li);
        return;
    }

    data.ingredients.forEach(i => {
        const li = document.createElement('li');
        li.textContent = i.name;
        groceryList.appendChild(li);
    });
}

function resetIngredientForm() {
    state.editingIngredientId = null;
    ingredientNameInput.value = '';
    ingredientMode.textContent = 'Add mode';
    ingredientSubmit.textContent = 'Add';
    ingredientCancel.classList.add('hidden');
}

function resetFoodForm() {
    state.editingFoodId = null;
    foodNameInput.value = '';
    setFoodIngredientSelection([]);
    document.querySelectorAll('#mealTypeGroup input[type=checkbox]').forEach(cb => (cb.checked = true));
    foodMode.textContent = 'Add mode';
    foodSubmit.textContent = 'Save Food';
    foodCancel.classList.add('hidden');
    foodEditorTitle.textContent = 'Food Editor';
}

function setFoodIngredientSelection(ids) {
    state.selectedFoodIngredientIds = (ids || []).map(Number);
    state.foodIngredientFilter = '';
    foodIngredientSearch.value = '';
    renderIngredientCheckboxes();
}

(async function init() {
    setStatus('Loading...');
    await loadIngredients();
    await loadFoods();
    await loadPlans();
    setStatus('Ready');
})();
