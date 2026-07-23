// pages/lesson-edit/lesson-edit.js
const db = require('../../utils/db');
const util = require('../../utils/util');
const app = getApp();

Page({
  data: {
    id: '',
    students: [],
    studentIndex: 0,
    selectedStudentName: '',
    subjects: [],
    subjectIndex: 0,
    statusOptions: [
      { value: 'pending', label: '待上课' },
      { value: 'completed', label: '已完成' }
    ],
    form: {
      studentId: '',
      subject: '',
      date: '',
      status: 'pending',
      objectives: [''],
      knowledgePoints: [''],
      exercises: [{ content: '' }],
      notes: ''
    }
  },

  onLoad(options) {
    this.setData({
      subjects: app.globalData.subjects
    });

    if (options.studentId) {
      this.setData({ 'form.studentId': options.studentId });
    }

    this.loadStudents();

    if (options.id) {
      this.setData({ id: options.id });
      this.loadLesson(options.id);
    } else {
      // 设置默认日期为今天
      this.setData({ 'form.date': util.getToday() });
    }
  },

  async loadStudents() {
    try {
      const students = await db.students.getAll();
      this.setData({ students });

      // 设置预选学生的索引
      if (this.data.form.studentId) {
        const studentIndex = students.findIndex(s => s._id === this.data.form.studentId);
        if (studentIndex >= 0) {
          this.setData({
            studentIndex,
            selectedStudentName: students[studentIndex].name
          });
        }
      }
    } catch (error) {
      console.error('加载学生列表失败:', error);
      util.showError('加载学生列表失败');
    }
  },

  async loadLesson(id) {
    util.showLoading('加载中...');
    try {
      const lesson = await db.lessons.getById(id);

      const studentIndex = this.data.students.findIndex(s => s._id === lesson.studentId);
      const subjectIndex = this.data.subjects.findIndex(s => s === lesson.subject);

      const exercises = lesson.exercises || [{ content: '' }];

      this.setData({
        form: {
          studentId: lesson.studentId,
          subject: lesson.subject,
          date: lesson.date,
          status: lesson.status || 'pending',
          objectives: lesson.objectives || [''],
          knowledgePoints: lesson.knowledgePoints || [''],
          exercises: exercises,
          notes: lesson.notes || ''
        },
        studentIndex: studentIndex >= 0 ? studentIndex : 0,
        selectedStudentName: studentIndex >= 0 ? this.data.students[studentIndex].name : '',
        subjectIndex: subjectIndex >= 0 ? subjectIndex : 0
      });
    } catch (error) {
      console.error('加载课程失败:', error);
      util.showError('加载失败');
    } finally {
      util.hideLoading();
    }
  },

  onStudentChange(e) {
    const index = e.detail.value;
    this.setData({
      studentIndex: index,
      selectedStudentName: this.data.students[index].name,
      'form.studentId': this.data.students[index]._id
    });
  },

  onSubjectChange(e) {
    const index = e.detail.value;
    this.setData({
      subjectIndex: index,
      'form.subject': this.data.subjects[index]
    });
  },

  onDateChange(e) {
    this.setData({ 'form.date': e.detail.value });
  },

  onStatusTap(e) {
    const value = e.currentTarget.dataset.value;
    this.setData({ 'form.status': value });
  },

  onAddObjective() {
    this.setData({
      'form.objectives': [...this.data.form.objectives, '']
    });
  },

  onObjectiveInput(e) {
    const index = e.currentTarget.dataset.index;
    const objectives = this.data.form.objectives;
    objectives[index] = e.detail.value;
    this.setData({ 'form.objectives': objectives });
  },

  onRemoveObjective(e) {
    const index = e.currentTarget.dataset.index;
    const objectives = this.data.form.objectives;
    if (objectives.length > 1) {
      objectives.splice(index, 1);
      this.setData({ 'form.objectives': objectives });
    }
  },

  onAddPoint() {
    this.setData({
      'form.knowledgePoints': [...this.data.form.knowledgePoints, '']
    });
  },

  onPointInput(e) {
    const index = e.currentTarget.dataset.index;
    const points = this.data.form.knowledgePoints;
    points[index] = e.detail.value;
    this.setData({ 'form.knowledgePoints': points });
  },

  onRemovePoint(e) {
    const index = e.currentTarget.dataset.index;
    const points = this.data.form.knowledgePoints;
    if (points.length > 1) {
      points.splice(index, 1);
      this.setData({ 'form.knowledgePoints': points });
    }
  },

  onAddExercise() {
    this.setData({
      'form.exercises': [...this.data.form.exercises, { content: '' }]
    });
  },

  onExerciseInput(e) {
    const index = e.currentTarget.dataset.index;
    const exercises = this.data.form.exercises;
    exercises[index].content = e.detail.value;
    this.setData({ 'form.exercises': exercises });
  },

  onRemoveExercise(e) {
    const index = e.currentTarget.dataset.index;
    const exercises = this.data.form.exercises;
    if (exercises.length > 1) {
      exercises.splice(index, 1);
      this.setData({ 'form.exercises': exercises });
    }
  },

  onNotesInput(e) {
    this.setData({ 'form.notes': e.detail.value });
  },

  validate() {
    if (!this.data.form.studentId) {
      util.showToast('请选择学生');
      return false;
    }
    if (!this.data.form.subject) {
      util.showToast('请选择科目');
      return false;
    }
    if (!this.data.form.date) {
      util.showToast('请选择日期');
      return false;
    }
    return true;
  },

  async onSubmit() {
    if (!this.validate()) return;

    util.showLoading('保存中...');
    try {
      const data = {
        studentId: this.data.form.studentId,
        subject: this.data.form.subject,
        date: this.data.form.date,
        status: this.data.form.status,
        objectives: this.data.form.objectives.filter(o => o.trim()),
        knowledgePoints: this.data.form.knowledgePoints.filter(p => p.trim()),
        exercises: this.data.form.exercises.filter(e => e.content.trim()),
        notes: this.data.form.notes.trim()
      };

      if (this.data.id) {
        await db.lessons.update(this.data.id, data);
      } else {
        await db.lessons.add(data);
      }

      await util.showSuccess('保存成功');
      util.navigateBack();
    } catch (error) {
      console.error('保存课程失败:', error);
      util.showError('保存失败');
    } finally {
      util.hideLoading();
    }
  },

  async onDelete() {
    const confirmed = await util.showConfirm('确定要删除这个课程吗？');
    if (!confirmed) return;

    try {
      await db.lessons.delete(this.data.id);
      await util.showSuccess('已删除');
      util.navigateBack();
    } catch (error) {
      console.error('删除课程失败:', error);
      util.showError('删除失败');
    }
  }
});
