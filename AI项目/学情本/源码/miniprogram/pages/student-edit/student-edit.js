// pages/student-edit/student-edit.js
const db = require('../../utils/db');
const util = require('../../utils/util');
const app = getApp();

Page({
  data: {
    id: '', // 编辑时有ID，添加时为空
    form: {
      name: '',
      grade: '',
      subjects: [],
      phone: '',
      notes: ''
    },
    grades: [],
    gradeIndex: 0,
    subjects: []
  },

  onLoad(options) {
    this.setData({
      grades: app.globalData.grades,
      subjects: app.globalData.subjects.map(s => ({
        name: s,
        selected: false
      }))
    });

    if (options.id) {
      this.setData({ id: options.id });
      this.loadStudent(options.id);
    }
  },

  async loadStudent(id) {
    util.showLoading('加载中...');
    try {
      const student = await db.students.getById(id);

      // 设置科目选中状态
      const subjects = this.data.subjects.map(s => ({
        ...s,
        selected: student.subjects.includes(s.name)
      }));

      // 设置年级索引
      const gradeIndex = this.data.grades.findIndex(g => g === student.grade);

      this.setData({
        form: {
          name: student.name,
          grade: student.grade,
          subjects: student.subjects || [],
          phone: student.phone || '',
          notes: student.notes || ''
        },
        gradeIndex,
        subjects
      });
    } catch (error) {
      console.error('加载学生失败:', error);
      util.showError('加载失败');
    } finally {
      util.hideLoading();
    }
  },

  onNameInput(e) {
    this.setData({ 'form.name': e.detail.value });
  },

  onGradeChange(e) {
    const index = e.detail.value;
    this.setData({
      gradeIndex: index,
      'form.grade': this.data.grades[index]
    });
  },

  onSubjectTap(e) {
    const index = e.currentTarget.dataset.index;
    const subjects = this.data.subjects;
    subjects[index].selected = !subjects[index].selected;

    this.setData({
      subjects,
      'form.subjects': subjects.filter(s => s.selected).map(s => s.name)
    });
  },

  onPhoneInput(e) {
    this.setData({ 'form.phone': e.detail.value });
  },

  onNotesInput(e) {
    this.setData({ 'form.notes': e.detail.value });
  },

  validate() {
    if (!this.data.form.name.trim()) {
      util.showToast('请输入学生姓名');
      return false;
    }
    if (!this.data.form.grade) {
      util.showToast('请选择年级');
      return false;
    }
    return true;
  },

  async onSubmit() {
    if (!this.validate()) return;

    util.showLoading('保存中...');
    try {
      const data = {
        name: this.data.form.name.trim(),
        grade: this.data.form.grade,
        subjects: this.data.form.subjects,
        phone: this.data.form.phone.trim(),
        notes: this.data.form.notes.trim()
      };

      if (this.data.id) {
        await db.students.update(this.data.id, data);
      } else {
        await db.students.add(data);
      }

      await util.showSuccess('保存成功');
      util.navigateBack();
    } catch (error) {
      console.error('保存学生失败:', error);
      util.showError('保存失败');
    } finally {
      util.hideLoading();
    }
  }
});